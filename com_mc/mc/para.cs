using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections;
using System.Runtime.InteropServices;

namespace com_mc
{
	public enum DataType //参数数据类型
	{
		u8, u16, u32, u64, //无符号整数
		s8, s16, s32, s64, //有符号整数
		f, df, //float、double
		undef, str, //未定义、字符串
		//插件扩展的时候，可以用undef
	}
	//参数定义，通过参数的唯一名称，或者唯一id实现访问
	public abstract class ParaValue //参数父类(无处理二进制块)
	{
		public string name { get; set; } = "";  //参数的唯一id，在C#程序中使用
		public int id { get; set; }=0; //参数的唯一id，在C程序中使用
		public DataType type { get; set; } = DataType.df; //参数类型,默认是double(在factory处设置)
		public int len { get; set; } = 0; //数据长度
		public List<string> str_tab { get; set; } = new List<string>();//显示字符串表。可用于bool型指令，0为失败字符，1为成果字符
		public ParaValue(Dictionary<string, object> v, DataType t) //从json构造对象
		{ //这里遇到错误就throw出去，不想throw的才判断
			if(v.ContainsKey("id")) id = (int)v["id"];
			name=(string)v["name"];
			type = t;
			if (v.ContainsKey("len")) len = (int)v["len"];
			else len = DATA_UNION.get_type_len(type);
			if (v.ContainsKey("str_tab"))
			{
				var ar = v["str_tab"] as ArrayList;
				for (int i = 0; i < ar.Count; i++) str_tab.Add(ar[i] as string);
				//str_tab = v["str_tab"] as string[];
			}
		}
		public virtual Dictionary<string, object> toJson() //输出json
		{
			var v = new Dictionary<string, object>();
			//v["id"]=id; //暂时不用
			v["name"]=name;
			if(type!=DataType.df) v["type"]=type;
			if(len!= DATA_UNION.get_type_len(type)) v["len"]=len;
			if (str_tab.Count > 0)
			{
				var ta = new ArrayList();
				for (int i = 0; i < str_tab.Count; i++)
				{
					ta.Add(str_tab[i]);
				}
				v["str_tab"] = ta;
			}
			return v;
		}
		public abstract int set_val(byte[] b, int off, int n); //从数据设定值,返回使用的字节数
		public abstract int get_val(byte[] b, int off, int n); //向数据缓存中复制数据,返回使用的字节数
		public virtual void set_val(double d) { } //设置double值
		public virtual void set_val(int d) { } //设置int值
		public virtual double get_val() { return 0; }
		public virtual int get_int() { return 0; }

		public delegate void CB(ParaValue pv);
		static public void void_cb(ParaValue pv) { }
		public CB update_cb= void_cb; //数据接收回调

		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		public static ParaValue factory(Dictionary<string, object> v) //构建工厂
		{
			DataType t = DataType.df; //默认是double
			if (v.ContainsKey("type"))
			{
				string s = json_ser.Serialize(v["type"]);
				t = json_ser.Deserialize<DataType>(s); //取得参数类型
			}
			switch (t)
			{
				case DataType.undef: 
				case DataType.str:
					return new ParaValue_Str(v, t);
				case DataType.u8:
				case DataType.u16:
				case DataType.u32:
				case DataType.u64:
				case DataType.s8:
				case DataType.s16:
				case DataType.s32:
				case DataType.s64:
				case DataType.f:
				case DataType.df:
					return new ParaValue_Val(v, t);
				default: throw new Exception("type err");
			}
		}
	}
	public class ParaValue_Str : ParaValue //字符型
	{
		public ParaValue_Str(Dictionary<string, object> v, DataType t) :base(v,t) { }
		public byte[] data { get; set; } = new byte[0]; //参数数据
		public string str_val=""; //参数值(若是字符类型)
		public override string ToString()
		{
			if (data.Length <= 0) return "no data";
			if (type == DataType.str) return Encoding.UTF8.GetString(data);
			else if (type == DataType.undef)
			{
				string s = "";
				for (int i = 0; i < data.Length; i++)
				{
					s += string.Format("{0:X00} ", data[i]);
				}
				return s;
			}
			else return "type err";
		}
		public override int set_val(byte[] b, int off, int n) //从数据设定值
		{
			int i = 0;
			if (len == 0) data = new byte[n]; //若没有指定长度，则使用外部长度
			else n = len > n ? n : len; //若指定了长度，需按比较小的那个长度来
			for (; i < data.Length && i < n && off < b.Length; i++)
			{
				data[i] = b[off]; off++;
			}
			update_cb(this);
			return i; //返回使用的字节数
		}
		public override int get_val(byte[] b, int off, int n) //向数据缓存中复制数据
		{
			int i = 0;
			for (; i < len && i < data.Length && i < n && off < b.Length; i++)
			{
				b[off] = data[i]; off++;
			}
			return i; //返回使用的字节数
		}
	}
	public class ParaValue_Val : ParaValue //值类型
	{
		public ParaValue_Val(Dictionary<string, object> v, DataType t) : base(v, t)
		{
			if(v.ContainsKey("point_n")) point_n = (int)v["point_n"];
		}
		public override Dictionary<string, object> toJson()
		{
			var v = base.toJson();
			if(point_n!=2) v["point_n"] = point_n;
			return v;
		}
		public DATA_UNION data=new DATA_UNION();
		public int point_n { get; set; } = 2;//小数位数
		public override string ToString() //默认显示函数
		{
			string s = "";
			if(str_tab.Count > 0) //若有字符表，要用字符显示。要求数据类型是整数
			{
				if(data.ds32<str_tab.Count)
				{
					return str_tab[data.ds32];
				}
				return "over table"; //返回超过表长
			}
			switch (type)
			{
				case DataType.u8: s = String.Format("{0}", data.du8); break;
				case DataType.u16: s = String.Format("{0}", data.du16); break;
				case DataType.u32: s = String.Format("{0}", data.du32); break;
				case DataType.u64: s = String.Format("{0}", data.du64); break;
				case DataType.s8: s = String.Format("{0}", data.ds8); break;
				case DataType.s16: s = String.Format("{0}", data.ds16); break;
				case DataType.s32: s = String.Format("{0}", data.ds32); break;
				case DataType.s64: s = String.Format("{0}", data.ds64); break;
				case DataType.f: s = data.f.ToString(string.Format("F{0}", point_n)); break;
				case DataType.df: s = data.df.ToString(string.Format("F{0}", point_n)); break;
				default: return "type err";
			}
			return s;
		}
		public override int set_val(byte[] b, int off, int n) //从数据设定值
		{
			int r = data.set_val(b, off, n);
			update_cb(this);
			return r;
		}
		public override int get_val(byte[] b, int off, int n) //向数据缓存中复制数据
		{
			return data.get_val(b, off, n);
		}
		public override void set_val(double d)
		{
			switch (type) //根据输出的类型给输出
			{
				case DataType.f: data.f = (float)d; break;
				case DataType.df: data.df = d; break;
				default:
					data.ds64 = ProtDom.double_2_s64(d); //转换成整数，四舍五入
					break;
			}
			update_cb(this); //需要调参数的回调函数
		}
		public override void set_val(int i)
		{
			switch (type) //根据输出的类型给输出
			{
				case DataType.f: data.f = i; break;
				case DataType.df: data.df = i; break;
				default:
					data.ds32 = i; //转换成整数，四舍五入
					break;
			}
			update_cb(this); //需要调参数的回调函数
		}
		public override double get_val()
		{
			return data.get_double(type);
		}
		public override int get_int()
		{
			return data.ds32;
		}
	}
	[StructLayout(LayoutKind.Explicit)]
	public struct DATA_UNION //各种值类型
	{
		//[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		[FieldOffset(0)] public byte du8; 
		[FieldOffset(1)] public byte du8_1; 
		[FieldOffset(2)] public byte du8_2; 
		[FieldOffset(3)] public byte du8_3; 
		[FieldOffset(4)] public byte du8_4; 
		[FieldOffset(5)] public byte du8_5; 
		[FieldOffset(6)] public byte du8_6; 
		[FieldOffset(7)] public byte du8_7; 

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
		public int set_val(byte[] b, int off, int n) //从数据设定值
		{
			du64 = 0;
			int i = 0;
			for (; i < 8 && i < n && off < b.Length; i++)
			{
				switch (i)
				{
					case 0: du8 = b[off]; break;
					case 1: du8_1 = b[off]; break;
					case 2: du8_2 = b[off]; break;
					case 3: du8_3 = b[off]; break;
					case 4: du8_4 = b[off]; break;
					case 5: du8_5 = b[off]; break;
					case 6: du8_6 = b[off]; break;
					case 7: du8_7 = b[off]; break;
					default:
						break;
				}
				off++;
			}
			return i; //返回使用的字节数
		}
		public int get_val(byte[] b, int off, int n) //向数据缓存中复制数据
		{
			int i = 0;
			for (;i < 8 && i < n && off < b.Length; i++)
			{
				switch (i)
				{
					case 0: b[off] = du8; break;
					case 1: b[off] = du8_1; break;
					case 2: b[off] = du8_2; break;
					case 3: b[off] = du8_3; break;
					case 4: b[off] = du8_4; break;
					case 5: b[off] = du8_5; break;
					case 6: b[off] = du8_6; break;
					case 7: b[off] = du8_7; break;
				}
				off++;
			}
			return i; //返回使用的字节数
		}
		static public int get_type_len(DataType t)
		{
			switch (t)
			{
				case DataType.u8: return 1;
				case DataType.u16: return 2;
				case DataType.u32: return 4;
				case DataType.u64: return 8;
				case DataType.s8: return 1;
				case DataType.s16: return 2;
				case DataType.s32: return 4;
				case DataType.s64: return 8;
				case DataType.f: return 4;
				case DataType.df: return 8;
				default: return 0;
			}
		}
		public double get_double(DataType t)
		{
			switch (t)
			{
				case DataType.u8: return du8;
				case DataType.u16: return du16;
				case DataType.u32: return du32;
				case DataType.u64: return du64;
				case DataType.s8: return ds8;
				case DataType.s16: return ds16;
				case DataType.s32: return ds32;
				case DataType.s64: return ds64;
				case DataType.f: return f;
				case DataType.str: //字符型的默认是按df来解
				case DataType.df: return df;
				default: throw new Exception("type err");
			}
		}
		public int get_int(DataType t)
		{
			switch (t)
			{
				case DataType.u8: return du8;
				case DataType.u16: return du16;
				case DataType.u32: return (int)du32;
				case DataType.u64: return (int)du64;
				case DataType.s8: return ds8;
				case DataType.s16: return ds16;
				case DataType.s32: return ds32;
				case DataType.s64: return (int)ds64;
				case DataType.f: return (int)f;
				case DataType.str: //字符型的默认是按df来解
				case DataType.df: return (int)df;
				default: return 0;
			}
		}
	}
}

