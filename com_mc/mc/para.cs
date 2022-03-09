using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Runtime.InteropServices;

namespace com_mc
{
	public enum ParaType //参数类型
	{
		undef, str, //未定义、字符串
		u8, u16, u32, u64, //无符号整数
		s8, s16, s32, s64, //有符号整数
		f, df //float、double
	}
	//参数定义，通过参数的唯一名称，或者唯一id实现访问
	public abstract class ParaValue //参数父类(无处理二进制块)
	{
		public string name { get; set; } = "";  //参数的唯一id，在C#程序中使用
		public int id { get; set; }=0; //参数的唯一id，在C程序中使用
		public ParaType type { get; set; } = 0; //参数类型
		public int len { get; set; } = 0; //数据长度
		public ParaValue(Dictionary<string, object> v,ParaType t) //从json构造对象
		{ //这里遇到错误就throw出去，不想throw的才判断
			if(v.ContainsKey("id")) id = (int)v["id"];
			name=(string)v["name"];
			type = t;
			len = (int)v["len"];
		}
		public abstract int set_val(byte[] b, int off, int n); //从数据设定值
		public abstract int get_val(byte[] b, int off, int n); //向数据缓存中复制数据

		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		public static ParaValue factory(Dictionary<string, object> v) //构建工厂
		{
			string s = json_ser.Serialize(v["type"]);
			ParaType t = json_ser.Deserialize<ParaType>(s); //取得参数类型
			switch (t)
			{
				case ParaType.undef: 
				case ParaType.str:
					return new ParaValue_Str(v, t);
				case ParaType.u8:
				case ParaType.u16:
				case ParaType.u32:
				case ParaType.u64:
				case ParaType.s8:
				case ParaType.s16:
				case ParaType.s32:
				case ParaType.s64:
				case ParaType.f:
				case ParaType.df:
					return new ParaValue_Val(v, t);
				default: throw new Exception("type err");
			}
		}
		public static Dictionary<string, ParaValue> para_dict = new Dictionary<string, ParaValue>(); //参数字典
	}
	public class ParaValue_Str : ParaValue //字符型
	{
		public ParaValue_Str(Dictionary<string, object> v, ParaType t) :base(v,t) { }
		public byte[] data { get; set; } = new byte[0]; //参数数据
		public override string ToString()
		{
			if (data.Length < len || len == 0) return "no data";
			if (type == ParaType.str) return Encoding.UTF8.GetString(data);
			else if (type == ParaType.undef)
			{
				string s = "";
				for (int i = 0; i < data.Length; i++)
				{
					s += string.Format("{0:X00} ", data[i]);
				}
				return Encoding.UTF8.GetString(data);
			}
			else return "type err";
		}
		public override int set_val(byte[] b, int off, int n) //从数据设定值
		{
			for (int i = 0; i < len && i < data.Length && i < n && off < b.Length; i++)
			{
				data[i] = b[off]; off++;
			}
			return 0;
		}
		public override int get_val(byte[] b, int off, int n) //向数据缓存中复制数据
		{
			for (int i = 0; i < len && i < data.Length && i < n && off < b.Length; i++)
			{
				b[off] = data[i]; off++;
			}
			return 0;
		}
	}
	public class ParaValue_Val : ParaValue //值类型
	{
		public ParaValue_Val(Dictionary<string, object> v, ParaType t) : base(v, t)
		{
			point_n = (int)v["point_n"];
		}
		public DATA_UNION data=new DATA_UNION() { du8=new byte[8]};
		public int point_n { get; set; } //小数位数
		public string[] str_tab { get; set; } = new string[0];//显示字符串表
		public string[] fmt_str = new string[]  //最多8位小数
		{
			"{0:0}",
			"{0:0.0}",
			"{0:0.00}",
			"{0:0.000}",
			"{0:0.0000}",
			"{0:0.00000}",
			"{0:0.0000000}",
			"{0:0.00000000}",
			"{0:0.000000000}",
		};
		public override string ToString() //默认显示函数
		{
			string s = "";
			if(str_tab.Length > 0) //若有字符表，要用字符显示
			{
				if(data.ds32<str_tab.Length)
				{
					return str_tab[data.ds32];
				}
				return "over table"; //返回超过表长
			}
			switch (type)
			{
				case ParaType.u8:
					s = String.Format("{0}", data.du8);
					break;
				case ParaType.u16:
					s = String.Format("{0}", data.du16);
					break;
				case ParaType.u32:
					s = String.Format("{0}", data.du32);
					break;
				case ParaType.u64:
					s = String.Format("{0}", data.du64);
					break;
				case ParaType.s8:
					s = String.Format("{0}", data.ds8);
					break;
				case ParaType.s16:
					s = String.Format("{0}", data.ds16);
					break;
				case ParaType.s32:
					s = String.Format("{0}", data.ds32);
					break;
				case ParaType.s64:
					s = String.Format("{0}", data.ds64);
					break;
				case ParaType.f:
					s = String.Format(fmt_str[point_n], data.f);
					break;
				case ParaType.df:
					s = String.Format(fmt_str[point_n], data.df);
					break;
				default: return "type err";
			}
			return s;
		}
		public override int set_val(byte[] b, int off, int n) //从数据设定值
		{
			for (int i = 0; i < len && i < 8 && i < n && off < b.Length; i++)
			{
				data.du8[i] = b[off]; off++;
			}
			return 0;
		}
		public override int get_val(byte[] b, int off, int n) //向数据缓存中复制数据
		{
			for (int i = 0; i < len && i < 8 && i < n && off < b.Length; i++)
			{
				b[off] = data.du8[i]; off++;
			}
			return 0;
		}
	}
	[StructLayout(LayoutKind.Explicit)]
	public struct DATA_UNION //各种值类型
	{
		[FieldOffset(0)]
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		public byte[] du8; 
		[FieldOffset(0)]
		public UInt16 du16;
		[FieldOffset(0)]
		public uint du32;
		[FieldOffset(0)]
		public UInt64 du64;
		[FieldOffset(0)]
		public sbyte ds8;
		[FieldOffset(0)]
		public Int16 ds16;
		[FieldOffset(0)]
		public int ds32;
		[FieldOffset(0)]
		public Int64 ds64;
		[FieldOffset(0)]
		public float f;
		[FieldOffset(0)]
		public double df;
	}
}

