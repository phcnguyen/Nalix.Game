﻿using Nalix.Game.Domain.Models.Maps.Zones;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nalix.Game.Domain.Models.Maps;

public class Map : IMap
{
    public int Id { get; set; }
    public long TimeMap { get; set; }
    public bool IsStop { get; set; }
    public bool IsRunning { get; set; }

    public Task HandleZone { get; set; }
    public TileMap TileMap { get; set; }
    public List<Zone> Zones { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public Map(int id, TileMap tileMap)
    {
        Id = id;
        Zones = [];
        TimeMap = -1;
        IsRunning = false;
        IsStop = false;
        TileMap = tileMap ?? throw new ArgumentNullException(nameof(tileMap));
    }

    public void SetZone()
    {
        for (var i = 0; i < TileMap.ZoneCount; i++)
        {
            Zones.Add(new Zone(i, this));
        }
    }

    public Zone GetZoneNotMaxPlayer()
        => Zones.FirstOrDefault(x => x.Characters.Count < TileMap.MaxPlayers);

    public Zone GetZonePlayer() => Zones.FirstOrDefault(x => !x.Characters.IsEmpty);
}