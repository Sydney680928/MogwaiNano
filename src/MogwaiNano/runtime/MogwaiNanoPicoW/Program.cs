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

using MogwaiNano.Engine;
using MogwaiNanoAdc;
using MogwaiNanoGpio;
using MogwaiNanoSsd1306;
using MowgaiNanoPwm;
using System.Threading;

namespace MogwaiNanoPicoW
{
    public class Program
    {
        public static void Main()
        {
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

            MogwaiNanoEngine.RegisterPrimitive("pwm.open", new(PwmManager.PrimitivePwmOpen));
            MogwaiNanoEngine.RegisterPrimitive("pwm.close", new(PwmManager.PrimitivePwmClose));
            MogwaiNanoEngine.RegisterPrimitive("pwm.start", new(PwmManager.PrimitivePwmStart));
            MogwaiNanoEngine.RegisterPrimitive("pwm.stop", new(PwmManager.PrimitivePwmStop));

            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmAlreadyOpenedError);
            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmOpenError);
            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmUnknownNameError);
            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmStartError);
            MogwaiNanoEngine.RegisterExternalError(PwmManager.PwmStopError);

            MogwaiNanoEngine.Skills.Add("PWM");

            #endregion

            #region ADC

            MogwaiNanoEngine.RegisterPrimitive("adc.open", new(AdcManager.PrimitiveAdcOpen));
            MogwaiNanoEngine.RegisterPrimitive("adc.close", new(AdcManager.PrimitiveAdcClose));
            MogwaiNanoEngine.RegisterPrimitive("adc.read", new(AdcManager.PrimitiveAdcReadValue));
            MogwaiNanoEngine.RegisterPrimitive("adc.resolutionInBits", new(AdcManager.PrimitiveAdcGetResolutionInBits));
            MogwaiNanoEngine.RegisterPrimitive("adc.maxValue", new(AdcManager.PrimitiveAdcGetMaxValue));

            MogwaiNanoEngine.RegisterExternalError(AdcManager.AdcAlreadyOpenedError);
            MogwaiNanoEngine.RegisterExternalError(AdcManager.AdcOpenError);
            MogwaiNanoEngine.RegisterExternalError(AdcManager.AdcUnknownNameError);

            MogwaiNanoEngine.Skills.Add("ADC");

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

            MogwaiNanoEngine.ResetDidExecute += (engine) =>
            {
                if (!engine.IsTask)
                {
                    if (!engine.KeepAlive)
                    {
                        GpioManager.Cleanup();
                    }
                }

                if (!engine.KeepAlive)
                {
                    SSD1306Manager.Cleanup(engine);
                    PwmManager.Cleanup(engine);
                    AdcManager.Cleanup(engine);
                }

            };

            MogwaiNanoCore.MogwaiNanoSystem.Start();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
