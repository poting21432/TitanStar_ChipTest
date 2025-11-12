cls
commander adapter dbgmode out
commander device masserase --device efr32
commander flash --tokengroup znet --token TOKEN_MFG_CTUNE:0x85 --device efr32
commander flash --tokengroup znet --tokenfile "%~dp0tokenFile.txt" --device efr32
commander tokendump --tokengroup znet --token TOKEN_MFG_STRING --token TOKEN_MFG_CTUNE --token TOKEN_MFG_BOARD_NAME --device efr32
commander flash "%~dp0ZB_RT050_V2.0.0.250422.s37" --device efr32
