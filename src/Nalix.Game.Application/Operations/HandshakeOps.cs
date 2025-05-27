﻿using Nalix.Common.Attributes;
using Nalix.Common.Connection;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Constants;
using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Common.Security;
using Nalix.Cryptography.Asymmetric;
using Nalix.Cryptography.Hashing;
using Nalix.Extensions.Primitives;
using Nalix.Logging;

namespace Nalix.Game.Application.Operations;

/// <summary>
/// Handles the secure handshake process for establishing encrypted connections using X25519 and ISHA.
/// This class manages both the initiation and finalization of secure connections with clients.
/// The class ensures secure communication by exchanging keys and validating them using X25519 and hashing via ISHA.
/// </summary>
[PacketController]
internal sealed class HandshakeOps<TPacket> where TPacket : IPacket, IPacketFactory<TPacket>
{
    /// <summary>
    /// Initiates a secure connection by performing a handshake with the client.
    /// Expects a binary packet containing the client's X25519 public key (32 bytes).
    /// </summary>
    /// <param name="packet">The incoming packet containing the client's public key.</param>
    /// <param name="connection">The connection to the client that is requesting the handshake.</param>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Moderate)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketOpcode((ushort)ProtocolCommand.StartHandshake)]
    [PacketRateLimit(RequestLimitType.Low)]
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> StartHandshake(IPacket packet, IConnection connection)
    {
        // CheckLimit if the packet type is binary (as expected for X25519 public key).
        if (packet.Type != PacketType.Binary)
        {
            NLogix.Host.Instance.Debug("Received non-binary packet [Type={0}] from {1}",
                           packet.Type, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ProtocolCommand.StartHandshake, "Invalid packet type")
                          .Serialize();
        }

        // Validate that the public key length is 32 bytes (X25519 standard).
        if (packet.Payload.Length != 32)
        {
            NLogix.Host.Instance.Debug("Invalid public key length [Length={0}] from {1}",
                           packet.Payload.Length, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ProtocolCommand.StartHandshake, ProtocolMessage.InvalidData)
                          .Serialize();
        }

        if (IsReplayAttempt(connection))
        {
            NLogix.Host.Instance.Debug("Detected handshake replay attempt from {0}", connection.RemoteEndPoint);
            return TPacket.Create((ushort)ProtocolCommand.CompleteHandshake, ProtocolMessage.RateLimited).Serialize();
        }

        // Generate an X25519 key pair (private and public keys).
        (byte[] @private, byte[] @public) = X25519.GenerateKeyPair();

        // Store the private key in connection metadata for later use.
        connection.Metadata[Meta.PrivateKey] = @private;
        connection.Metadata[Meta.LastHandshakeTime] = System.DateTime.UtcNow;

        // Derive the shared secret key using the server's private key and the client's public key.
        connection.EncryptionKey = DeriveSharedKey(@private, packet.Payload.ToArray());

        // Elevate the client's access level after successful handshake initiation.
        connection.Level = PermissionLevel.User;

        // SendPacket the server's public key back to the client for the next phase of the handshake.
        return TPacket.Create(
            (ushort)ProtocolCommand.StartHandshake,
            PacketType.Binary, PacketFlags.None, PacketPriority.Low, @public).Serialize();
    }

    /// <summary>
    /// Finalizes the secure connection by verifying the client's public key and comparing it to the derived encryption key.
    /// This method ensures the integrity of the handshake process by performing key comparison.
    /// </summary>
    /// <param name="packet">The incoming packet containing the client's public key for finalization.</param>
    /// <param name="connection">The connection to the client.</param>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Moderate)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketOpcode((ushort)ProtocolCommand.CompleteHandshake)]
    [PacketRateLimit(RequestLimitType.Low)]
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> CompleteHandshake(IPacket packet, IConnection connection)
    {
        // Ensure the packet type is binary (expected for public key).
        if (packet.Type != PacketType.Binary)
        {
            NLogix.Host.Instance.Debug("Received non-binary packet [Type={0}] from {1}",
                           packet.Type, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ProtocolCommand.CompleteHandshake, ProtocolMessage.InvalidData).Serialize();
        }

        // CheckLimit if the public key length is correct (32 bytes).
        if (packet.Payload.Length != 32)
        {
            NLogix.Host.Instance.Debug("Invalid public key length [Length={0}] from {1}",
                           packet.Payload.Length, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ProtocolCommand.CompleteHandshake, ProtocolMessage.InvalidPayload).Serialize();
        }

        // Retrieve the stored private key from connection metadata.
        if (!connection.Metadata.TryGetValue(Meta.PrivateKey, out object privateKeyObj) ||
            privateKeyObj is not byte[] @private)
        {
            NLogix.Host.Instance.Debug("Missing or invalid private key for {0}", connection.RemoteEndPoint);

            return TPacket.Create((ushort)ProtocolCommand.CompleteHandshake, ProtocolMessage.UnknownError).Serialize();
        }

        // Derive the shared secret using the private key and the client's public key.
        byte[] derivedKey = DeriveSharedKey(@private, packet.Payload.ToArray());

        connection.Metadata.Remove(Meta.PrivateKey);

        // Compare the derived key with the encryption key in the connection.
        if (connection.EncryptionKey is null || !connection.EncryptionKey.IsEqualTo(derivedKey))
        {
            NLogix.Host.Instance.Debug("Key mismatch during handshake finalization for {0}", connection.RemoteEndPoint);
            return TPacket.Create(
                (ushort)ProtocolCommand.CompleteHandshake, ProtocolMessage.Conflict).Serialize();
        }

        NLogix.Host.Instance.Debug("Secure connection established for {0}", connection.RemoteEndPoint);
        return TPacket.Create(
                (ushort)ProtocolCommand.CompleteHandshake,
                ProtocolMessage.Success).Serialize();
    }

    #region Private Methods

    /// <summary>
    /// Computes a derived encryption key by performing the X25519 key exchange and then hashing the result.
    /// This method produces a shared secret by combining the client's public key and the server's private key,
    /// followed by hashing the result using the specified hashing algorithm.
    /// </summary>
    /// <param name="privateKey">The server's private key used in the key exchange.</param>
    /// <param name="publicKey">The client's public key involved in the key exchange.</param>
    /// <returns>The derived encryption key, which is used to establish a secure connection.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static byte[] DeriveSharedKey(byte[] privateKey, byte[] publicKey)
    {
        // Perform the X25519 key exchange to derive the shared secret.
        byte[] secret = X25519.ComputeSharedSecret(privateKey, publicKey);

        // Hash the shared secret to produce the final encryption key.
        return SHA256.HashData(secret);
    }

    /// <summary>
    /// Checks if the connection is attempting to replay a previous handshake.
    /// </summary>
    /// <param name="connection"></param>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsReplayAttempt(IConnection connection)
    {
        if (connection.Metadata.TryGetValue(Meta.LastHandshakeTime,
            out object lastTimeObj) && lastTimeObj is System.DateTime lastTime)
            return (System.DateTime.UtcNow - lastTime).TotalSeconds < 10;

        return false;
    }

    private static class Meta
    {
        public const string PrivateKey = "X25519_PrivateKey";
        public const string LastHandshakeTime = "LastHandshakeTime";
    }

    #endregion Private Methods
}