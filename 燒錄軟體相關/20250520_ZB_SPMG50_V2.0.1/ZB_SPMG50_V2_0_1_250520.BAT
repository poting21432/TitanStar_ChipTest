cls
commander adapter dbgmode out
commander flash --tokengroup znet --token TOKEN_MFG_CTUNE:0x6b --device efr32
commander flash --tokengroup znet --tokenfile "%~dp0tokenFile.txt" --device efr32
commander tokendump --tokengroup znet --token TOKEN_MFG_STRING --token TOKEN_MFG_BOARD_NAME --token TOKEN_MFG_CTUNE --device efr32
commander flash "%~dp020250520_ZB_SPMG50_V2.0.1.s37" --device efr32