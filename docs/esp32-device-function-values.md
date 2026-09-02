# ESP32 `DeviceFunction` Values — Reference for `device.setPinFunction`

Extracted from `nanoFramework.Hardware.Esp32`'s `DeviceFunction` enum (v1.6.42). These are the numeric values to pass as the `function` parameter to `device.setPinFunction pin function`.

This applies to the classic `ESP32_REV3` target. Other ESP32 variants (like `ESP32_S3_OCTAL`) may not follow the same default pin mapping at all — on those boards, `device.setPinFunction` may be required even for buses (like I2C) that need no configuration whatsoever on `ESP32_REV3`.

## Default pin mapping on `ESP32_REV3`

Before reaching for `device.setPinFunction`, check whether the pin you need is already wired by default — no configuration needed in that case. This is nanoFramework's own default mapping, straight from the [official ESP32 Pin Out documentation](https://docs.nanoframework.net/content/esp32/esp32_pin_out.html).

### I2C (already wired by default)

| Bus | Data | Clock |
|---|---|---|
| I2C1 | GPIO 18 | GPIO 19 |
| I2C2 | GPIO 25 | GPIO 26 |

This is exactly why `ESP32_REV3` never needs `device.setPinFunction` for I2C — the default bus is already usable as-is with `i2c.open`.

### SPI (already wired by default)

| Bus | MOSI | MISO | Clock |
|---|---|---|---|
| SPI1 | GPIO 23 | GPIO 25 | GPIO 19 |
| SPI2 | undefined | undefined | undefined |

### Serial (COM)

| Port | TX | RX | RTS | CTS |
|---|---|---|---|---|
| COM1 (reserved for debugging when enabled) | GPIO 1 | GPIO 3 | GPIO 19 | GPIO 22 |
| COM2 | undefined | undefined | undefined | undefined |
| COM3 | undefined | undefined | undefined | undefined |

### PWM — undefined by default

All 16 PWM channels have no pin assigned at startup — `device.setPinFunction` is always required to use PWM on `ESP32_REV3`. Channels `PWM0`-`PWM7` use a low-precision timer; `PWM8`-`PWM15` use a high-resolution timer — pick based on the precision your use case needs (a passive buzzer doesn't care much; driving something more time-sensitive might).

### ADC — fixed GPIO mapping, no `device.setPinFunction` needed

Unlike PWM, ADC channels map to fixed GPIOs and don't need `device.setPinFunction` — just pass the right channel number to `adc.open`.

| Channel | Internal controller | GPIO | Notes |
|---|---|---|---|
| 0 | ADC1 | 36 | See restrictions below |
| 1 | ADC1 | 37 | |
| 2 | ADC1 | 38 | |
| 3 | ADC1 | 39 | See restrictions below |
| 4 | ADC1 | 32 | |
| 5 | ADC1 | 33 | |
| 6 | ADC1 | 34 | |
| 7 | ADC1 | 35 | |
| 8 | ADC1 | 36 | Internal temperature sensor (VP) — see restrictions |
| 9 | ADC1 | 39 | Internal Hall sensor (VN) — see restrictions |
| 10 | ADC2 | 4 | Unavailable while WiFi is active |
| 11 | ADC2 | 0 | Strapping pin — unavailable while WiFi is active |
| 12 | ADC2 | 2 | Strapping pin — unavailable while WiFi is active |
| 13 | ADC2 | 15 | Strapping pin — unavailable while WiFi is active |
| 14 | ADC2 | 13 | Unavailable while WiFi is active |
| 15 | ADC2 | 12 | Unavailable while WiFi is active |
| 16 | ADC2 | 14 | Unavailable while WiFi is active |
| 17 | ADC2 | 27 | Unavailable while WiFi is active |
| 18 | ADC2 | 25 | Unavailable while WiFi is active |
| 19 | ADC2 | 26 | Unavailable while WiFi is active |

**Restrictions:**
- **Channels 10-19 (all `ADC2`) can't be used while WiFi is active** — throws `CLR_E_PIN_UNAVAILABLE`. Since MOGWAI NANO relies on WiFi for its entire connectivity model, **channels 0-9 (all `ADC1`) are the only practical choice** for any real project
- The Hall sensor (channel 9) and temperature sensor (channel 8) can't be used at the same time as channels 0 and 3
- GPIO 0, 2, 15 (channels 11-13) are strapping pins — check your board's schematic before using them for anything, boot behavior can be affected

### DAC

| DAC | GPIO |
|---|---|
| DAC1 | 25 |
| DAC2 | 26 |

---

## `DeviceFunction` numeric values (for `device.setPinFunction`)

## SPI

| Function | Value |
|---|---|
| SPI1 MOSI | 65792 |
| SPI1 MISO | 65793 |
| SPI1 CLOCK | 65794 |
| SPI2 MOSI | 66048 |
| SPI2 MISO | 66049 |
| SPI2 CLOCK | 66050 |

## I2C

| Function | Value |
|---|---|
| I2C1 DATA | 131328 |
| I2C1 CLOCK | 131329 |
| I2C2 DATA | 131584 |
| I2C2 CLOCK | 131585 |

## Serial (COM)

| Function | Value |
|---|---|
| COM1 TX | 196864 |
| COM1 RX | 196865 |
| COM1 RTS (Request to Send) | 196866 |
| COM1 CTS (Clear to Send) | 196867 |
| COM2 TX | 197120 |
| COM2 RX | 197121 |
| COM2 RTS | 197122 |
| COM2 CTS | 197123 |
| COM3 TX | 197376 |
| COM3 RX | 197377 |
| COM3 RTS | 197378 |
| COM3 CTS | 197379 |
| COM4 TX | 197632 |
| COM4 RX | 197633 |
| COM4 RTS | 197634 |
| COM4 CTS | 197635 |

## PWM

| Function | Value |
|---|---|
| PWM1 | 262400 |
| PWM2 | 262656 |
| PWM3 | 262912 |
| PWM4 | 263168 |
| PWM5 | 263424 |
| PWM6 | 263680 |
| PWM7 | 263936 |
| PWM8 | 264192 |
| PWM9 | 264448 |
| PWM10 | 264704 |
| PWM11 | 264960 |
| PWM12 | 265216 |
| PWM13 | 265472 |
| PWM14 | 265728 |
| PWM15 | 265984 |
| PWM16 | 266240 |

> Reminder: PWM channels work in pairs sharing the same frequency (e.g. PWM1/PWM2). For two independent frequencies, use channels at least 2 apart.

## ADC

| Function | Value |
|---|---|
| ADC1 channel 0 | 327936 |
| ADC1 channel 1 | 327937 |
| ADC1 channel 2 | 327938 |
| ADC1 channel 3 | 327939 |
| ADC1 channel 4 | 327940 |
| ADC1 channel 5 | 327941 |
| ADC1 channel 6 | 327942 |
| ADC1 channel 7 | 327943 |
| ADC1 channel 8 (internal temperature sensor, VP) | 327944 |
| ADC1 channel 9 (internal Hall sensor, VN) | 327945 |
| ADC1 channel 10 (internally ESP32 ADC2 channel 10) | 327946 |
| ADC1 channel 11 (internally ESP32 ADC2 channel 11) | 327947 |
| ADC1 channel 12 (internally ESP32 ADC2 channel 12) | 327948 |
| ADC1 channel 13 (internally ESP32 ADC2 channel 13) | 327949 |
| ADC1 channel 14 (internally ESP32 ADC2 channel 14) | 327950 |
| ADC1 channel 15 (internally ESP32 ADC2 channel 15) | 327951 |
| ADC1 channel 16 (internally ESP32 ADC2 channel 16) | 327952 |
| ADC1 channel 17 (internally ESP32 ADC2 channel 17) | 327953 |
| ADC1 channel 18 (internally ESP32 ADC2 channel 18) | 327954 |
| ADC1 channel 19 (internally ESP32 ADC2 channel 19) | 327955 |

## I2S

| Function | Value |
|---|---|
| I2S1 Master Clock (master mode only) | 393472 |
| I2S1 Bit Clock (general purpose read/write) | 393473 |
| I2S1 WS (stereo) | 393474 |
| I2S1 DATA_OUT (typically a speaker) | 393475 |
| I2S1 MDATA_IN (typically a microphone) | 393476 |
| I2S2 Master Clock (master mode only) | 393728 |
| I2S2 Bit Clock | 393729 |
| I2S2 WS (stereo) | 393730 |
| I2S2 DATA_OUT | 393731 |
| I2S2 MDATA_IN | 393732 |

## SDMMC

| Function | Value |
|---|---|
| SDMMC1 clock | 524544 |
| SDMMC1 command | 524545 |
| SDMMC1 data D0 | 524546 |
| SDMMC1 data D1 | 524547 |
| SDMMC1 data D2 | 524548 |
| SDMMC1 data D3 | 524549 |
| SDMMC2 clock | 524800 |
| SDMMC2 command | 524801 |
| SDMMC2 data D0 | 524802 |
| SDMMC2 data D1 | 524803 |
| SDMMC2 data D2 | 524804 |
| SDMMC2 data D3 | 524805 |
