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
		public string fname_time_fmt = "yyyyMMdd_HHmmss"; //时间格式
		public string ts_fmt = "mmss.fff	"; //用于时间戳的时间格式，回放约定的格式
		public DirectoryInfo basepath = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "/data/"); //记录文件基础路径
		public string suffix = ".txt"; //后缀
		public uint restart_T = 60 * 5; //重新建立文件的周期(s)

		public uint pre_utc = 0;//文件创建时的utc秒数
		public string filename = ""; //记录文件的全路径（只读）
		public TextDataFile()
		{
		}
		public void create()
		{
			//建立文件夹
			if (!Directory.Exists(basepath.FullName))
			{
				Directory.CreateDirectory(basepath.FullName);
			}
			close();
			//建立文件
			pre_utc = (uint)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			sw = new StreamWriter(filename, true);
		}
		~TextDataFile()
		{
			if (sw != null && sw.BaseStream.Length <= 0) delete();
			close();
		}
		public void close()
		{
			if (sw == null) return;
			try
			{
				sw.Close();
				sw = null;
			}
			catch { }
		}
		public void update_file_name()
		{
			//uint t = (uint)(DateTime.Now.Ticks / 10000000); //表示0001年1月1日午夜 12:00:00 以来所经历的 100 纳秒数
			uint t = (uint)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uint t_h = (t / restart_T) * restart_T; //整数记录点
			if (pre_utc < t_h || sw == null) //到了整数时间或文件还没建立
			{
				DateTime cur_time = DateTime.Now;
				filename = basepath.FullName + "/" + cur_time.ToString(fname_time_fmt) + suffix;
				create();
			}
		}
		public void write(string s)
		{
			update_file_name();
			if (!s.EndsWith("\n")) s += "\n";
			//时间戳
			string tmp_time = DateTime.Now.ToString(ts_fmt); //时间戳
			s = tmp_time + s;
			sw.Write(s);
			sw.Flush();
		}
		public void delete()//删除文件
		{
			sw.Close();
			File.Delete(filename);
		}
	}
}
