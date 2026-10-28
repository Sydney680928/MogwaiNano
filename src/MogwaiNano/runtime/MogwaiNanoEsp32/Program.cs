// Copyright 2026 Stéphane Sibué
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using MowgaiNanoPwm;
using MogwaiNano;
using MogwaiNano.Engine;
using MogwaiNano.Objects;
using MogwaiNanoBlePeripheral;
using MogwaiNanoCore;
using MogwaiNanoEsp32LED;
using MogwaiNanoGpio;
using MogwaiNanoSsd1306;
using MogwaiNanoTft;
using MogwaiNanoTouch;
using System;
using System.Diagnostics;
using System.Threading;
using static MogwaiNano.Engine.MogwaiNanoEngine;
using MogwaiNanoAdc;
using MogwaiNanoDateTime;

namespace MogwaiNanoEsp32
{
    public class Program
    {
        public static void Main()
        {
            MogwaiNanoEngine.RegisterPrimitive("esp32.setPinFunction", new(PrimitiveDeviceSetPinFunction), true);

            #region GPIO

            MogwaiNanoEngine.RegisterPrimitive("gpio.setMode.input", new(GpioManager.PrimitiveGpioModeInput));
            MogwaiNanoEngine.RegisterPrimitive("gpio.setMode.inputPullDown", new(GpioManager.PrimitiveGpioSetModeInputPullDown));
            MogwaiNanoEngine.RegisterPrimitive("gpio.setMode.inputPullUp", new(GpioManager.PrimitiveGpioSetModeInputPullUp));
            MogwaiNanoEngine.RegisterPrimitive("gpio.setMode.output", new(GpioManager.PrimitiveGpioSetModeOutput));
            MogwaiNanoEngine.RegisterPrimitive("gpio.read", new(GpioManager.PrimitiveGpioPinRead));
            MogwaiNanoEngine.RegisterPrimitive("gpio.write.high", new(GpioManager.PrimitiveGpioPinWriteHigh));
            MogwaiNanoEngine.RegisterPrimitive("gpio.write.low", new(GpioManager.PrimitiveGpioPinWriteLow));
            MogwaiNanoEngine.RegisterPrimitive("gpio.toggle", new(GpioManager.PrimitiveGpioPinToggle));
            MogwaiNanoEngine.RegisterPrimitive("gpio.close", new(GpioManager.PrimitiveGpioPinClose));

            MogwaiNanoEngine.Skills.Add("GPIO");

            #endregion

            #region PWM

            MogwaiNanoEngine.RegisterPrimitive("pwm.open", new (PwmManager.PrimitivePwmOpen));
            MogwaiNanoEngine.RegisterPrimitive("pwm.close", new (PwmManager.PrimitivePwmClose));
            MogwaiNanoEngine.RegisterPrimitive("pwm.start", new (PwmManager.PrimitivePwmStart));
            MogwaiNanoEngine.RegisterPrimitive("pwm.stop", new (PwmManager.PrimitivePwmStop));

            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmAlreadyOpenedError);
            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmOpenError);
            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmUnknownNameError);
            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmStartError);
            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmStopError);

            MogwaiNanoEngine.Skills.Add("PWM");

            #endregion

            #region ADC

            MogwaiNanoEngine.RegisterPrimitive("adc.open", new (AdcManager.PrimitiveAdcOpen));
            MogwaiNanoEngine.RegisterPrimitive("adc.close", new (AdcManager.PrimitiveAdcClose));
            MogwaiNanoEngine.RegisterPrimitive("adc.read", new (AdcManager.PrimitiveAdcReadValue));
            MogwaiNanoEngine.RegisterPrimitive("adc.resolutionInBits", new (AdcManager.PrimitiveAdcGetResolutionInBits));
            MogwaiNanoEngine.RegisterPrimitive("adc.maxValue", new (AdcManager.PrimitiveAdcGetMaxValue));

            MogwaiNanoEngine.RegisterExternalError(AdcManager.AdcAlreadyOpenedError);   
            MogwaiNanoEngine.RegisterExternalError(AdcManager.AdcOpenError);    
            MogwaiNanoEngine.RegisterExternalError(AdcManager.AdcUnknownNameError);

            MogwaiNanoEngine.Skills.Add("ADC");

            #endregion

            #region BLE PERIPHERAL

            MogwaiNanoEngine.RegisterPrimitive("ble.peripheral.create", new(BlePeripheral.PrimitiveBlePeripheralCreate));
            MogwaiNanoEngine.RegisterPrimitive("ble.peripheral.start", new(BlePeripheral.PrimitiveBlePeripheralStart));
            MogwaiNanoEngine.RegisterPrimitive("ble.peripheral.stop", new(BlePeripheral.PrimitiveBlePeripheralStop));
            MogwaiNanoEngine.RegisterPrimitive("ble.peripheral.getValue", new(BlePeripheral.PrimitiveBlePeripheralGetValue));
            MogwaiNanoEngine.RegisterPrimitive("ble.peripheral.setValue", new(BlePeripheral.PrimitiveBlePeripheralSetValue));
            MogwaiNanoEngine.RegisterPrimitive("ble.peripheral.notify", new(BlePeripheral.PrimitiveBlePeripheralNotify));

            MogwaiNanoEngine.RegisterExternalError(BlePeripheral.BlePeripheralUnableToCreateError);

            MogwaiNanoEngine.Skills.Add("BLE-P");

            #endregion

            #region SSD1306

            MogwaiNanoEngine.RegisterPrimitive("ssd1306.init", new(SSD1306Manager.PrimitiveSsd1306Init));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.close", new(SSD1306Manager.PrimitiveSsd1306Close));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.clear", new(SSD1306Manager.PrimitiveSsd1306Clear));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.printString", new(SSD1306Manager.PrimitiveSsd1306PrintString));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.drawString", new(SSD1306Manager.PrimitiveSsd1306DrawString));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.refresh", new(SSD1306Manager.PrimitiveSsd1306Refresh));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.drawPixel", new(SSD1306Manager.PrimitiveSsd1306DrawPixel));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.drawHorizontalLine", new(SSD1306Manager.PrimitiveSsd1306DrawHorizontalLine));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.drawVerticalLine", new(SSD1306Manager.PrimitiveSsd1306DrawHVerticalLine));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.drawRectangle", new(SSD1306Manager.PrimitiveSsd1306DrawRectangle));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.drawFilledRectangle", new(SSD1306Manager.PrimitiveSsd1306DrawFilledRectangle));
            MogwaiNanoEngine.RegisterPrimitive("ssd1306.drawBitmap", new(SSD1306Manager.PrimitiveSsd1306DrawBitmap));

            MogwaiNanoEngine.RegisterExternalError(SSD1306Manager.SSD1306InitError);
            MogwaiNanoEngine.RegisterExternalError(SSD1306Manager.SSD1306IsClosedError);
            MogwaiNanoEngine.RegisterExternalError(SSD1306Manager.SSD1306IsOpenedError);
            MogwaiNanoEngine.RegisterExternalError(SSD1306Manager.SSD1306OperationError);

            MogwaiNanoEngine.Skills.Add("SSD1306");

            #endregion

            #region TFT

            // SPI pins are set from here so that the TFT library stays platform independent.

            TftManager.ConfigureSpiPins = (bus, sclk, mosi, miso) =>
            {
                if (bus != 1)
                    throw new NotSupportedException("only SPI bus 1 is supported");

                nanoFramework.Hardware.Esp32.Configuration.SetPinFunction(mosi, nanoFramework.Hardware.Esp32.DeviceFunction.SPI1_MOSI);

                if (miso >= 0)
                    nanoFramework.Hardware.Esp32.Configuration.SetPinFunction(miso, nanoFramework.Hardware.Esp32.DeviceFunction.SPI1_MISO);

                nanoFramework.Hardware.Esp32.Configuration.SetPinFunction(sclk, nanoFramework.Hardware.Esp32.DeviceFunction.SPI1_CLOCK);
            };

            MogwaiNanoEngine.RegisterPrimitive("tft.init", new(TftManager.PrimitiveTftInit));
            MogwaiNanoEngine.RegisterPrimitive("tft.close", new(TftManager.PrimitiveTftClose));
            MogwaiNanoEngine.RegisterPrimitive("tft.setRotation", new(TftManager.PrimitiveTftSetRotation));
            MogwaiNanoEngine.RegisterPrimitive("tft.width", new(TftManager.PrimitiveTftWidth));
            MogwaiNanoEngine.RegisterPrimitive("tft.height", new(TftManager.PrimitiveTftHeight));
            MogwaiNanoEngine.RegisterPrimitive("tft.backlight", new(TftManager.PrimitiveTftBacklight));
            MogwaiNanoEngine.RegisterPrimitive("tft.refresh", new(TftManager.PrimitiveTftRefresh));
            MogwaiNanoEngine.RegisterPrimitive("tft.autoRefresh", new(TftManager.PrimitiveTftAutoRefresh));
            MogwaiNanoEngine.RegisterPrimitive("tft.color", new(TftManager.PrimitiveTftColor));
            MogwaiNanoEngine.RegisterPrimitive("tft.clear", new(TftManager.PrimitiveTftClear));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawPixel", new(TftManager.PrimitiveTftDrawPixel));
            MogwaiNanoEngine.RegisterPrimitive("tft.getPixel", new(TftManager.PrimitiveTftGetPixel));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawHorizontalLine", new(TftManager.PrimitiveTftDrawHorizontalLine));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawVerticalLine", new(TftManager.PrimitiveTftDrawVerticalLine));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawLine", new(TftManager.PrimitiveTftDrawLine));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawRectangle", new(TftManager.PrimitiveTftDrawRectangle));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawFilledRectangle", new(TftManager.PrimitiveTftDrawFilledRectangle));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawRoundRectangle", new(TftManager.PrimitiveTftDrawRoundRectangle));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawFilledRoundRectangle", new(TftManager.PrimitiveTftDrawFilledRoundRectangle));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawCircle", new(TftManager.PrimitiveTftDrawCircle));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawFilledCircle", new(TftManager.PrimitiveTftDrawFilledCircle));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawTriangle", new(TftManager.PrimitiveTftDrawTriangle));
            MogwaiNanoEngine.RegisterPrimitive("tft.scroll", new(TftManager.PrimitiveTftScroll));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawBitmap", new(TftManager.PrimitiveTftDrawBitmap));
            MogwaiNanoEngine.RegisterPrimitive("tft.smooth", new(TftManager.PrimitiveTftSmooth));
            MogwaiNanoEngine.RegisterPrimitive("tft.drawString", new(TftManager.PrimitiveTftDrawString));
            MogwaiNanoEngine.RegisterPrimitive("tft.measureString", new(TftManager.PrimitiveTftMeasureString));

            MogwaiNanoEngine.RegisterExternalError(TftManager.TftInitError);
            MogwaiNanoEngine.RegisterExternalError(TftManager.TftIsClosedError);
            MogwaiNanoEngine.RegisterExternalError(TftManager.TftIsOpenedError);
            MogwaiNanoEngine.RegisterExternalError(TftManager.TftOperationError);

            MogwaiNanoEngine.Skills.Add("TFT");

            #endregion

            #region TOUCH

            // I2C pins are set from here so that the touch library stays platform independent.

            TouchManager.ConfigureI2cPins = (bus, sda, scl) =>
            {
                if (bus != 1)
                    throw new NotSupportedException("only I2C bus 1 is supported");

                nanoFramework.Hardware.Esp32.Configuration.SetPinFunction(sda, nanoFramework.Hardware.Esp32.DeviceFunction.I2C1_DATA);
                nanoFramework.Hardware.Esp32.Configuration.SetPinFunction(scl, nanoFramework.Hardware.Esp32.DeviceFunction.I2C1_CLOCK);
            };

            // The touch coordinates follow the rotation of the TFT screen, when there is one.
            TouchManager.GetRotation = () => TftManager.Rotation;

            MogwaiNanoEngine.RegisterPrimitive("touch.init", new(TouchManager.PrimitiveTouchInit));
            MogwaiNanoEngine.RegisterPrimitive("touch.close", new(TouchManager.PrimitiveTouchClose));
            MogwaiNanoEngine.RegisterPrimitive("touch.start", new(TouchManager.PrimitiveTouchStart));
            MogwaiNanoEngine.RegisterPrimitive("touch.stop", new(TouchManager.PrimitiveTouchStop));
            MogwaiNanoEngine.RegisterPrimitive("touch.count", new(TouchManager.PrimitiveTouchCount));
            MogwaiNanoEngine.RegisterPrimitive("touch.read", new(TouchManager.PrimitiveTouchRead));
            MogwaiNanoEngine.RegisterPrimitive("touch.readPoint", new(TouchManager.PrimitiveTouchReadPoint));

            MogwaiNanoEngine.RegisterExternalError(TouchManager.TouchInitError);
            MogwaiNanoEngine.RegisterExternalError(TouchManager.TouchIsClosedError);
            MogwaiNanoEngine.RegisterExternalError(TouchManager.TouchIsOpenedError);
            MogwaiNanoEngine.RegisterExternalError(TouchManager.TouchOperationError);

            MogwaiNanoEngine.Skills.Add("TOUCH");

            #endregion

            #region ONBOARD LED

            MogwaiNanoEngine.RegisterPrimitive("onboard.led.set", new(LedManager.PrimitiveLedSet));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.off", new(LedManager.PrimitiveLedOff));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.green", new(LedManager.PrimitiveLedGreen));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.red", new(LedManager.PrimitiveLedRed));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.blue", new(LedManager.PrimitiveLedBlue));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.orange", new(LedManager.PrimitiveLedOrange));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.yellow", new(LedManager.PrimitiveLedYellow));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.purple", new(LedManager.PrimitiveLedPurple));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.brightness", new(LedManager.PrimitiveLedBrightness));
            MogwaiNanoEngine.RegisterPrimitive("onboard.led.release", new(LedManager.PrimitiveLedRelease));

            MogwaiNanoEngine.RegisterExternalError(LedManager.LedOperationError);

            MogwaiNanoEngine.Skills.Add("ONBOARD-LED");

            #endregion

            #region DATE TIME

            MogwaiNanoEngine.RegisterPrimitive("setDateTime", new(DateTimeManager.PrimitiveSetDateTime));
            MogwaiNanoEngine.RegisterPrimitive("now", new(DateTimeManager.PrimitiveNow));
            MogwaiNanoEngine.RegisterPrimitive("date->", new(DateTimeManager.PrimitiveDateToRecord));
            MogwaiNanoEngine.RegisterPrimitive("->date", new(DateTimeManager.PrimitiveRecordToDate));

            MogwaiNanoEngine.Skills.Add("DATE-TIME");

            #endregion

            MogwaiNanoEngine.ResetDidExecute += (engine) =>
            {
                if (!engine.IsTask)
                {
                    if (!engine.KeepAlive)
                    {
                        GpioManager.Cleanup();
                        TftManager.Cleanup();
                        TouchManager.Cleanup();
                    }
                }

                if (!engine.KeepAlive)
                {
                    SSD1306Manager.Cleanup(engine);
                    LedManager.Cleanup(engine);
                    PwmManager.Cleanup(engine);
                    AdcManager.Cleanup(engine);
                }

            };

            MogwaiNanoSystem.MogwaiNanoLedSettings += (gpio) =>
            {
                if (gpio > -1)
                {
                    LedManager.Initialize(gpio);

                    MogwaiNano.AppGlobal.RuntimeStateChanged += (state) =>
                    {
                        if (state == 0)
                        {
                            LedManager.SetState(LedState.Off);
                        }
                        else if (state == 1)
                        {
                            LedManager.SetState(LedState.Initializing);
                        }
                        else if (state == 2)
                        {
                            LedManager.SetState(LedState.Idle);
                        }
                        else if (state == 3)
                        {
                            LedManager.SetState(LedState.Running);
                        }
                        else if (state == 4)
                        {
                            LedManager.SetState(LedState.Paused);
                        }
                        else if (state == 5)
                        {
                            LedManager.SetState(LedState.Error);
                        }
                    };

                    MogwaiNanoEngine.ProgramDidStart += (engine) =>
                    {
                        MogwaiNano.AppGlobal.SetRuntimeState(3);
                    };

                    MogwaiNanoEngine.ProgramDidEnd += (engine, result) =>
                    {
                        if (result.IsError)
                        {
                            MogwaiNano.AppGlobal.SetRuntimeState(5);
                        }
                        else
                        {
                            MogwaiNano.AppGlobal.SetRuntimeState(2);
                        }
                    };

                    MogwaiNanoEngine.ProgramDidPause += (engine) =>
                    {
                        MogwaiNano.AppGlobal.SetRuntimeState(4);
                    };

                    MogwaiNanoEngine.ProgramDidResume += (engine) =>
                    {
                        MogwaiNano.AppGlobal.SetRuntimeState(3);
                    };
                }
            };

            MogwaiNanoCore.MogwaiNanoSystem.Start();

            Thread.Sleep(Timeout.Infinite);
        }

        private static EvalResult PrimitiveDeviceSetPinFunction(MogwaiNanoEngine engine, string name)
        {
            // pin setvalue device.setPin

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var function = engine.StackPop() as MOGNumber;
                var pin = engine.StackPop() as MOGNumber;

                if (pin.Value < 0)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                try
                {
                    nanoFramework.Hardware.Esp32.Configuration.SetPinFunction((int)pin.Value, (nanoFramework.Hardware.Esp32.DeviceFunction)(int)function.Value);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.PlatformNotSupportedError, name, ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }
    }
}