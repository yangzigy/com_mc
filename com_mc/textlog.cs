using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace com_mc
{
	public class TextDataFile //存储二进制数据的文件
	{
		StreamWriter sw=null;
		string logpath="";
		string cur_time = "";
		public TextDataFile()
		{
		}
		public void create()
		{
			//建立文件夹
			if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "/" + "data"))
			{
				Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "/" + "data");
			}
			//建立文件
			cur_time = DateTime.Now.ToString("yyyy-MM-dd_HH");
			logpath = AppDomain.CurrentDomain.BaseDirectory + "/data/" +cur_time+  ".txt";
			sw= new StreamWriter(logpath,true);
			//sw.WriteLine("时间	温度	板温	功率	状态");
		}
		~TextDataFile()
		{
			close();
		}
		public void close()
		{
			if (sw == null) return;
			try
			{
				sw.Close();
			}
			catch { }
		}
		public void write(string s)
		{
			string tmp_time;
			tmp_time = DateTime.Now.ToString("yyyy-MM-dd_HH"); //每小时一个文件
			if (tmp_time != cur_time)
			{
				close();
				create();
			}
			tmp_time = DateTime.Now.ToString("HHmmss	"); //时间戳
			sw.Write(tmp_time + s);
			sw.Flush();
		}
		public void delete()//删除文件
		{
			sw.Close();
			File.Delete(logpath);
		}
	}
}
