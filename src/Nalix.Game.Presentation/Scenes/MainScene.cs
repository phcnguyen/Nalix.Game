﻿using Nalix.Graphics;
using Nalix.Graphics.Rendering.Object;
using Nalix.Graphics.Rendering.Parallax;
using Nalix.Graphics.Scenes;
using Nalix.Network.Package;
using Nalix.Shared.Net;
using SFML.Audio;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Game.Presentation.Scenes;

/// <summary>
/// Cảnh chính hiển thị sau khi người chơi kết nối thành công.
/// Bao gồm nền parallax và nút thiết lập.
/// </summary>
internal class MainScene : Scene
{
    public MainScene() : base(SceneNames.Main)
    {
    }

    /// <summary>
    /// Tải các đối tượng hiển thị trong cảnh chính.
    /// </summary>
    protected override void LoadObjects()
    {
        AddObject(new ParallaxLayer());  // Hiệu ứng nền chuyển động nhiều lớp
        AddObject(new SettingIcon());    // Biểu tượng thiết lập (setting)
    }

    #region Private Class

    /// <summary>
    /// Đối tượng Menu (hiện tại chưa xử lý gì – placeholder).
    /// </summary>
    [IgnoredLoad("RenderObject")]
    public class Menu : RenderObject
    {
        public Menu()
        {
            base.SetZIndex(1); // Ưu tiên vẽ sau nền
        }

        public override void Update(float deltaTime)
        { }

        public override void Render(RenderTarget target)
        { }

        protected override Drawable GetDrawable() => null;
    }

    /// <summary>
    /// Lớp hiệu ứng nền parallax gồm nhiều lớp ảnh cuộn với tốc độ khác nhau.
    /// </summary>
    [IgnoredLoad("RenderObject")]
    private class ParallaxLayer : RenderObject
    {
        private readonly ParallaxBackground _parallax;

        public ParallaxLayer()
        {
            base.SetZIndex(1);

            _parallax = new ParallaxBackground(GameEngine.ScreenSize);

            // Thêm các lớp nền từ xa đến gần (xa cuộn chậm, gần cuộn nhanh)
            _parallax.AddLayer(Assets.UiTextures.Load("bg/1"), 00f, true);
            _parallax.AddLayer(Assets.UiTextures.Load("bg/2"), 25f, true);
            _parallax.AddLayer(Assets.UiTextures.Load("bg/3"), 30f, true);
            _parallax.AddLayer(Assets.UiTextures.Load("bg/4"), 35f, true);
            _parallax.AddLayer(Assets.UiTextures.Load("bg/5"), 40f, true);
            _parallax.AddLayer(Assets.UiTextures.Load("bg/6"), 45f, true);
            _parallax.AddLayer(Assets.UiTextures.Load("bg/7"), 50f, true);
        }

        public override void Update(float deltaTime) => _parallax.Update(deltaTime);

        protected override Drawable GetDrawable()
            => throw new System.NotSupportedException("Sử dụng phương thức Render() thay vì GetDrawable().");

        public override void Render(RenderTarget target)
        {
            if (!Visible) return;
            _parallax.Draw(target);
        }
    }

    /// <summary>
    /// Biểu tượng thiết lập cho phép chuyển đến Scene Settings.
    /// </summary>
    [IgnoredLoad("RenderObject")]
    private class SettingIcon : RenderObject
    {
        private readonly Sound _clickSound;
        private readonly Sprite _settingsIcon;

        public SettingIcon()
        {
            base.SetZIndex(2); // Luôn hiển thị phía trên các lớp nền

            // Tải texture biểu tượng thiết lập
            Texture texture = Assets.UiTextures.Load("icons/3.png");

            _settingsIcon = new Sprite(texture)
            {
                Scale = new Vector2f(2f, 2f),
                Color = new Color(255, 255, 180), // Tông vàng nhẹ
            };

            // Canh phải trên màn hình
            FloatRect bounds = _settingsIcon.GetGlobalBounds();
            _settingsIcon.Position = new Vector2f(GameEngine.ScreenSize.X - bounds.Width + 20, -10);

            // Âm thanh khi nhấn
            SoundBuffer buffer = Assets.Sounds.Load("1.wav");
            _clickSound = new Sound(buffer);
        }

        public override void Update(float deltaTime)
        {
            if (!Visible) return;

            // Nếu mất kết nối → trở về cảnh Network để kết nối lại
            if (!NetClient<Packet>.Instance.IsConnected)
            {
                SceneManager.ChangeScene(SceneNames.Network);
            }

            // Phím tắt (key S) để vào phần thiết lập
            if (InputState.IsKeyDown(Keyboard.Key.S))
            {
                _clickSound.Play();
                SceneManager.ChangeScene(SceneNames.Settings);
            }

            // Click chuột trái vào biểu tượng thiết lập
            if (InputState.IsMouseButtonPressed(Mouse.Button.Left))
            {
                if (_settingsIcon.GetGlobalBounds().Contains(InputState.GetMousePosition()))
                {
                    _clickSound.Play();
                    SceneManager.ChangeScene(SceneNames.Settings);
                }
            }
        }

        protected override Drawable GetDrawable() => _settingsIcon;
    }

    #endregion Private Class
}