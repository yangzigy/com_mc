using System;
using System.Collections.Generic;
using cslib;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;

namespace com_mc
{
	public interface Sync_interfase //帧同步接口
	{
		void pro(byte[] b, int off, int n); //数据输入接口
	}
	public class Sync_head : Frame_Sync, Sync_interfase //帧头同步
	{
		public int len_dom_type=0; //确定长度数据的类型
		public int len_dom_off=0; //确定长度数据的偏移位置
		public int len_dom_k = 1; //确定长度数据变换参数：y=kx+b
		public int len_dom_b = 0; //确定长度数据变换参数：y=kx+b
		public Dictionary<int, int> len_dict = new Dictionary<int, int>(); //确定长度数据表，用于包类型与包长的对应
		public void fromJson(Dictionary<string,object> v)
		{

		}
		public void pro(byte[] b, int off, int n)
		{
			for (int i = 0; i < n; i++)
			{
				rec_byte(b[i + off]);
			}
		}
		public override int pre_pack(byte[] b, int len)
		{
			return base.pre_pack(b, len);
		}
		public override int pro_pack(byte[] b, int len)
		{
			return base.pro_pack(b, len);
		}
	}
	public class Sync_Line : Line_Sync, Sync_interfase //分行处理
	{
		public void fromJson(Dictionary<string, object> v)
		{

		}
		public void pro(byte[] b, int off, int n)
		{
			for (int i = 0; i < n; i++)
			{
				rec_byte(b[i + off]);
			}
		}
		public override bool pro_pack(byte[] b, int len)
		{
			return base.pro_pack(b, len);
		}
	}
}

