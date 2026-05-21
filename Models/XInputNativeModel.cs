using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WPF_Translator_Screen.Models
{
    public partial class XInputNativeModel : IDisposable
    {

        private Controller _controller;
        private GamepadButtonFlags _prevButtons;
        private System.Threading.Timer _gamepadTimer;

        // Callback ที่ MainWindow จะส่งมาให้
        private readonly Action _onStartSelection;
        private readonly Action _onTranslate;

        // กำหนด button ที่ต้องการ (ปรับตามต้องการ)
        private const GamepadButtonFlags BUTTON_START_SELECTION = GamepadButtonFlags.LeftShoulder;  // LB
        private const GamepadButtonFlags BUTTON_TRANSLATE = GamepadButtonFlags.RightShoulder; // RB

        public XInputNativeModel(Action onStartSelection, Action onTranslate)
        {
            _onStartSelection = onStartSelection;
            _onTranslate = onTranslate;
            InitGamepad();
        }

        private bool InitGamepad()
        {
            _controller = new Controller(UserIndex.One);

            if (!_controller.IsConnected)
                return false;

            _prevButtons = 0;
            _gamepadTimer = new System.Threading.Timer(_ => PollGamepad(), null, 0, 50);
            return true;
        }

        private void PollGamepad()
        {
            if (!_controller.IsConnected) return;

            var state = _controller.GetState();
            var buttons = state.Gamepad.Buttons;

            // ตรวจจับ "กดใหม่" (rising edge) ไม่ใช่ค้างไว้
            var justPressed = buttons & ~_prevButtons;

            // ลูกศรซ้าย + Up ค้างอยู่ และเพิ่งกด LB
            bool holdingCombo = buttons.HasFlag(GamepadButtonFlags.DPadLeft) &&
                                buttons.HasFlag(GamepadButtonFlags.DPadUp);


            if (holdingCombo && justPressed.HasFlag(GamepadButtonFlags.LeftShoulder))
                _onStartSelection?.Invoke();

            if (holdingCombo && justPressed.HasFlag(GamepadButtonFlags.RightShoulder))
                _onTranslate?.Invoke();

            _prevButtons = buttons;
        }

        public void Dispose()
        {
            _gamepadTimer?.Dispose();
        }   
    }
}
