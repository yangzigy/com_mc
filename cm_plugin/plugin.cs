using System;

namespace com_mc
{
	//��ͨ��MainWindow.mwȡ�����з���Ȩ
	public class CM_Plugin : CM_Plugin_Interface
	{
		public override void send_cmd(string s)
		{
			base.send_cmd(s);
			//Console.Write(s); //���ʾ�����ػ���ָ��  Console�޷���ӡ
		}
	}
}
