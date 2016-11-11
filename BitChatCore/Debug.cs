﻿/*
Technitium Bit Chat
Copyright (C) 2015  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;

namespace BitChatCore
{
    public static class Debug
    {
        #region variables

        static object _lockObj = new object();
        static IDebug _debug;

        #endregion

        #region public static

        public static void SetDebug(IDebug debug)
        {
            _debug = debug;
        }

        public static void Write(string source, Exception ex)
        {
            if (_debug != null)
            {
                lock (_lockObj)
                {
                    _debug.Write(DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss") + "\t" + source + "\t" + ex.Message + "\r\nStack Trace: \r\n\t" + ex.StackTrace + "\r\n\r\n");
                }
            }
        }

        public static void Write(string source, string message)
        {
            if (_debug != null)
            {
                lock (_lockObj)
                {
                    _debug.Write(DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss") + "\t" + source + "\t" + message + "\r\n\r\n");
                }
            }
        }

        #endregion
    }
}
