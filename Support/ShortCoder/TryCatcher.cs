using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Support.Logger;

namespace Support
{
    public static class TryCatcher
    {
        public static bool TryCatch(this string ErrorTitle, Action Work, Action? FinallyWork = null)
        {
            try
            {
                Work?.Invoke();
                return true;
            }catch(Exception e)
            {
                SysLog.Add(LogLevel.Error, string.Format("{0}錯誤:{1}", ErrorTitle ,e.Message));
                return false;
            }
            finally
            {
                FinallyWork?.Invoke();
            }
        }
    }
}
