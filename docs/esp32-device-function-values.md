# ESP32 `DeviceFunction` Values — Reference for `device.setPinFunction`

Extracted from `nanoFramework.Hardware.Esp32`'s `DeviceFunction` enum (v1.6.42). These are the numeric values to pass as the `function` parameter to `device.setPinFunction pin function`.

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
