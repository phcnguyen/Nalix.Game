﻿using Nalix.Game.Presentation.Enums;
using Nalix.Graphics;
using Nalix.Graphics.Rendering.Object;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Game.Presentation.Objects;

/// <summary>
/// Đại diện cho một hộp thông báo trong giao diện người dùng của trò chơi.
/// Hộp thông báo hiển thị một thông điệp văn bản và có thể bao gồm một nút bấm tùy chọn.
/// </summary>
[IgnoredLoad("RenderObject")]
public class NotificationBox : RenderObject
{
    private readonly Sprite _button;
    private readonly Text _buttonText;
    private readonly Text _messageText;
    private readonly Sprite _background;

    private float _hoverTime = 0f;
    private bool _isHovering = false;

    /// <summary>
    /// Khởi tạo một hộp thông báo với thông điệp ban đầu và vị trí hiển thị.
    /// </summary>
    /// <param name="initialMessage">Thông điệp ban đầu hiển thị trong hộp thông báo. Mặc định là chuỗi rỗng.</param>
    /// <param name="side">Vị trí hiển thị của hộp thông báo (Top hoặc Bottom). Mặc định là Bottom.</param>
    public NotificationBox(string initialMessage = "", Side side = Side.Bottom)
    {
        Font font = Assets.Font.Load("1");
        Texture bgTexture = Assets.UiTextures.Load("dialog/7");

        float floatY;

        // Background
        if (side == Side.Bottom)
        {
            floatY = GameEngine.ScreenSize.Y * 0.6f;
        }
        else
        {
            floatY = GameEngine.ScreenSize.Y * 0.1f;
        }

        _background = new Sprite(bgTexture)
        {
            Scale = new Vector2f(0.8f, 0.8f),
            Position = new Vector2f((GameEngine.ScreenSize.X - (0.8f * bgTexture.Size.X)) / 2f, floatY)
        };

        // Text
        _messageText = new Text(
            WrapText(font, initialMessage, 20, bgTexture.Size.X * 0.7f), font, 20)
        {
            FillColor = Color.Black,
        };

        // Lấy thông tin kích thước của text để căn giữa
        FloatRect textBounds = _messageText.GetLocalBounds();

        // Đặt gốc về giữa text (cho phép Position là tâm)
        _messageText.Origin = new Vector2f(
            textBounds.Left + (textBounds.Width / 2f),
            textBounds.Top + (textBounds.Height / 2f)
        );

        // Đặt vị trí text vào chính giữa background
        _messageText.Position = new Vector2f(
            _background.Position.X + (_background.GetGlobalBounds().Width / 2f),
            _background.Position.Y + (_background.GetGlobalBounds().Height / 2f)
        );

        if (side == Side.Bottom)
        {
            _button = new Sprite(Assets.UiTextures.Load("button/7"))
            {
                Scale = new Vector2f(0.5f, 0.5f)
            };

            float buttonY = _messageText.Position.Y + _messageText.GetLocalBounds().Height + 5f;

            // Center button horizontally below the message text
            _button.Position = new Vector2f(
                _background.Position.X +
                (_background.GetGlobalBounds().Width / 2f) -
                (_button.GetGlobalBounds().Width / 2f),
                buttonY
            );

            _buttonText = new Text("Ok", font, 18)
            {
                FillColor = Color.White
            };

            this.CenterTextOnSprite();
        }

        base.Reveal();
        base.SetZIndex(ZIndex.Notification.ToInt());
    }

    /// <summary>
    /// Cập nhật thông điệp hiển thị trong hộp thông báo.
    /// </summary>
    /// <param name="newMessage">Thông điệp mới cần hiển thị.</param>
    public void UpdateMessage(string newMessage)
    {
        string wrappedText = WrapText(
            _messageText.Font, newMessage, _messageText.CharacterSize, _background.Texture.Size.X * 0.7f);

        _messageText.DisplayedString = wrappedText;

        // Cập nhật lại căn giữa sau khi thay đổi text
        FloatRect textBounds = _messageText.GetLocalBounds();

        _messageText.Origin = new Vector2f(
            textBounds.Left + (textBounds.Width / 2f),
            textBounds.Top + (textBounds.Height / 2f)
        );

        _messageText.Position = new Vector2f(
            _background.Position.X + (_background.GetGlobalBounds().Width / 2f),
            _background.Position.Y + (_background.GetGlobalBounds().Height / 2f)
        );
    }

    /// <summary>
    /// Cập nhật trạng thái của hộp thông báo, xử lý tương tác chuột (hover, click).
    /// </summary>
    /// <param name="deltaTime">Thời gian trôi qua kể từ lần cập nhật trước (tính bằng giây).</param>
    public override void Update(float deltaTime)
    {
        if (!Visible || _button is null) return;

        Vector2i mousePos = InputState.GetMousePosition();
        bool hover = _button.GetGlobalBounds().Contains(mousePos.X, mousePos.Y);

        if (hover)
        {
            _hoverTime += deltaTime;

            if (_hoverTime >= 0.1f)
            {
                if (!_isHovering)
                {
                    _isHovering = true;
                    _button.Texture = Assets.UiTextures.Load("button/8"); // Change to hover texture
                }
            }
        }
        else
        {
            if (_isHovering)
            {
                _isHovering = false;
                _button.Texture = Assets.UiTextures.Load("button/7"); // Change back to normal texture
            }
            _hoverTime = 0f;
        }

        // Check click
        if (InputState.IsMouseButtonPressed(Mouse.Button.Left) && _isHovering)
        {
            base.Conceal();
        }
    }

    /// <summary>
    /// Vẽ hộp thông báo lên mục tiêu render.
    /// </summary>
    /// <param name="target">Mục tiêu render (thường là cửa sổ trò chơi).</param>
    public override void Render(RenderTarget target)
    {
        if (!Visible) return;

        target.Draw(_background);
        target.Draw(_messageText);

        if (_button != null)
        {
            target.Draw(_button);
            target.Draw(_buttonText);
        }
    }

    /// <summary>
    /// Lấy đối tượng Drawable (không được hỗ trợ, sử dụng Render() thay thế).
    /// </summary>
    /// <returns>Không trả về giá trị, luôn ném ngoại lệ.</returns>
    /// <exception cref="System.NotSupportedException">Ném ra khi phương thức này được gọi.</exception>
    protected override Drawable GetDrawable()
        => throw new System.NotSupportedException("Use Render() instead.");

    /// <summary>
    /// Bọc văn bản để đảm bảo nó vừa với chiều rộng tối đa được chỉ định.
    /// </summary>
    /// <param name="font">Font chữ sử dụng cho văn bản.</param>
    /// <param name="text">Chuỗi văn bản cần bọc.</param>
    /// <param name="characterSize">Kích thước ký tự của văn bản.</param>
    /// <param name="maxWidth">Chiều rộng tối đa của văn bản.</param>
    /// <returns>Chuỗi văn bản đã được bọc dòng.</returns>
    private static string WrapText(Font font, string text, uint characterSize, float maxWidth)
    {
        string result = "";
        string currentLine = "";
        string[] words = text.Split(' ');

        foreach (var word in words)
        {
            string testLine = currentLine.Length > 0 ? currentLine + " " + word : word;

            Text tempText = new(testLine, font, characterSize);
            if (tempText.GetLocalBounds().Width > maxWidth)
            {
                result += currentLine + "\n";
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        result += currentLine;
        return result;
    }

    /// <summary>
    /// Căn giữa văn bản trên nút bấm.
    /// </summary>
    private void CenterTextOnSprite()
    {
        FloatRect textBounds = _buttonText.GetLocalBounds();
        FloatRect spriteBounds = _button.GetGlobalBounds();

        // Set the origin of the text to its center
        _buttonText.Origin = new Vector2f(
            textBounds.Left + (textBounds.Width / 2f),
            textBounds.Top + (textBounds.Height / 2f)
        );

        // Position the text at the center of the sprite
        _buttonText.Position = new Vector2f(
            spriteBounds.Left + (spriteBounds.Width / 2f),
            spriteBounds.Top + (spriteBounds.Height / 2f)
        );
    }
}