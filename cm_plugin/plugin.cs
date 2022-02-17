using System;

namespace com_mc
{
	//可通过MainWindow.mw取得所有访问权
	public class CM_Plugin : CM_Plugin_Interface
	{
		public override void send_cmd(string s)
		{
			base.send_cmd(s);
			//Console.Write(s); //插件示范，截获发送指令  Console无法打印
		}
	}
}
