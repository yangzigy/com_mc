using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace lgd_gui
{
	static public class Mingw
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int DllcallBack(IntPtr p, int n); //发送回调函数

		[DllImport(@"cmc_plugin.so",CallingConvention = CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
		public static extern int so_ini(DllcallBack pf); //初始化，注册回调函数

		[DllImport(@"cmc_plugin.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void so_cmd(string s); //发送指令

		[DllImport(@"cmc_plugin.so",CallingConvention = CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
		public static extern string so_rx(byte a); //接收数据函数,返回收到数据的通用格式，若没有收到则为空

		[DllImport(@"cmc_plugin.so",CallingConvention = CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
		public static extern string so_poll_100(); //周期调用,100Hz,返回额外的传感数据

		//public DllcallBack tx_cb; //tx_cb = new Mingw.DllcallBack(so_tx_cb);
	}
}
