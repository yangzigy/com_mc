using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using cslib;

namespace com_mc
{
	public class PD_Node : ProtDom //协议域叶子节点
	{
		public DATA_UNION data = new DATA_UNION(); //被协议域引用的时候可以直接用
		public int skip_n = 0; //处理完此列后，额外向后跳的字节数（文本行协议是列数）。例如文本行的一列要处理出多个参数，此处可填0。或者需要跳过一列，此处可填1
		public double pro_k { get; set; } = 1; //处理变换kx+b
		public double pro_b { get; set; } = 0;//处理变换kx+b
		public int bit_st { get; set; } = 0; //起始bit
		public int bit_len { get; set; } = 0; //bit长度，用此配置表示此域为按bit处理
		public int bit_singed { get; set; } = 0; //是否是有符号数
		public List<ParaValue> para_need_update = null; //增量协议中缓存更新的域
		public PD_Node(Dictionary<string, object> v, ProtType t, PD_Obj pd) : base(v, t,pd)
		{
			if(v.ContainsKey("pro_k")) pro_k = (double)(decimal)v["pro_k"];
			if(v.ContainsKey("pro_b")) pro_b = (double)(decimal)v["pro_b"];
			if (v.ContainsKey("bit_st")) bit_st = (int)v["bit_st"]; //默认从0bit开始
			if (v.ContainsKey("bit_len")) bit_len = (int)v["bit_len"];
			if (v.ContainsKey("bit_singed")) bit_singed = (int)v["bit_singed"]; //默认从0bit开始
			if (v.ContainsKey("skip_n")) skip_n = (int)v["skip_n"];
			len = DATA_UNION.get_type_len((DataType)type);
			//更新输入结构
			v["len"] = len;
		}
		//输入二进制值 处理成对应的类型后变换，然后根据输出类型，构造对应的二进制数，若有浮点变整型，需要四舍五入，通过set_val二进制接口传出  
		public override int pro(byte[] b, ref int off, int n)  //n:off之后还有多长，off：数据起始位置
		{
			if(len>n) return 1; //若输入数量不够，不解，返回未完成，下次从新解
			int i = data.set_val(b, off, len); //按字节为本类型的数据赋值
			off += i + skip_n; //本协议域解析完成后需要往后加多少字节（改为结构体输入后废弃，以后用skip类型）
			set_para_val(); //根据取得的值，变换，设置所关联参数数值
			return 0;
		}
		public void set_para_val() //设置参数值，根据bit_len判断是从df取得还是从du64取得。
		{
			//做运算：根据不同的输入类型，都转换成double来做运算。
			double d = 0;
			if (bit_len > 0) //若配了bit长度，说明是按位处理
			{
				data.du64 >>= bit_st;
				data.du64 &= masktab[bit_len];
				if (bit_singed != 0) //若是有符号数，需要给符号位
				{
					int shift_n = 64 - bit_len;
					data.du64 <<= shift_n;
					data.ds64 >>= shift_n;
					d = data.ds64;
				}
				d = data.du64;
			}
			else d = data.get_double((DataType)type); //整数或浮点都能兼容
			d = d * pro_k + pro_b;
			//最后给引用的参数
			ParaValue_Val p = (ParaValue_Val)ref_para; //二进制为值类型，输出也必然是值类型
			p.set_val(d);
			if (para_need_update != null) para_need_update.Add(p);
		}
		static UInt64[] masktab = new UInt64[] //bit位数的掩码，0~64bit
		{
			0,
			1, 3, 7, 0xf, 0x1f, 0x3f, 0x7f,0xff,
			0x1ff,0x3ff,0x7ff,0xfff, 0x1fff,0x3fff,0x7fff,0xffff,
			0x1ffff,0x3ffff,0x7ffff,0xfffff, 0x1fffff,0x3fffff,0x7fffff,0xffffff,
			0x1ffffff,0x3ffffff,0x7ffffff,0xfffffff, 0x1fffffff,0x3fffffff,0x7fffffff,0xffffffff,
			0x1ffffffff,0x3ffffffff,0x7ffffffff,0xfffffffff, 0x1fffffffff,0x3fffffffff,0x7fffffffff,0xffffffffff,
			0x1ffffffffff,0x3ffffffffff,0x7ffffffffff,0xfffffffffff, 0x1fffffffffff,0x3fffffffffff,0x7fffffffffff,0xffffffffffff,
			0x1ffffffffffff,0x3ffffffffffff,0x7ffffffffffff,0xfffffffffffff, 0x1fffffffffffff,0x3fffffffffffff,0x7fffffffffffff,0xffffffffffffff,
			0x1ffffffffffffff,0x3ffffffffffffff,0x7ffffffffffffff,0xfffffffffffffff, 0x1fffffffffffffff,0x3fffffffffffffff,0x7fffffffffffffff,0xffffffffffffffff,
		};
	}
	//输入为字符型的叶子节点（同时适用于二进制和文本行协议），分为十进制和hex。
	//输出可为值，也可直接为串
	public class PD_Str : PD_Node
	{
		public string str_type = ""; //空为10进制，hex：16进制
		public PD_Str(Dictionary<string, object> v, ProtType t, PD_Obj pd) : base(v, t,pd)
		{
			if(v.ContainsKey("str_type")) str_type=v["str_type"] as string;
		}
		public override int pro(byte[] b, ref int off, int n) //在二进制流中取得字符
		{
			int str_len = len;
			if (str_len == 0) //不确定字符串长度
			{
				for (; str_len < n; str_len++)
				{
					if (b[str_len + off] == 0) break;
				}
				if (str_len == n) return 1;//没找到，不完整，下次从新解
			}
			if (str_len > n) return 1;//没找到，不完整，下次从新解
			if (ref_para.type == DataType.undef || ref_para.type == DataType.str) //若输出是串
			{
				var tref = ref_para as ParaValue_Str;
				tref.set_val(b, off, str_len);
				off += str_len;
				if (para_need_update != null) para_need_update.Add(tref);
				return 0;
			}
			string s = Encoding.UTF8.GetString(b, off, str_len);
			off += str_len;
			pro_str(s);
			return 0;
		}
		public void pro_str(string s) //处理字符串输入
		{
			if (ref_para.type == DataType.undef || ref_para.type == DataType.str) //串型的输出
			{
				byte[] vs = Encoding.UTF8.GetBytes(s);
				var tref = ref_para as ParaValue_Str;
				tref.set_val(vs, 0, vs.Length);
				if (para_need_update != null) para_need_update.Add(tref);
			}
			else //值类型
			{
				if (str_type == "hex") //若是hex型字符串
				{
					data.du64 = UInt64.Parse(s, System.Globalization.NumberStyles.HexNumber);
				}
				else if (bit_len > 0) //若配了bit长度，说明是按位处理，需要按整数取而不是浮点
				{
					data.ds64 = Int64.Parse(s);
				}
				else data.df = double.Parse(s); //无论是否是整数，只要按10进制来取就行，当浮点取
				set_para_val();
			}
		}
	}
	public enum CheckMode //校验域类型
	{
		fix8,fix16,fix32,fix64, //固定数
		sum8,sum16,
		crc16,mdcrc16,crc32,
	}
	public class PD_Check : PD_Node //协议域叶子节点，校验功能
	{
		public string str_type = ""; //空为10进制，hex：16进制
		public CheckMode mode = CheckMode.fix8; //校验模式
		public int st_pos = 0; //计算起始偏移
		public UInt64 fix = 0; //默认是0
		public PD_Check(Dictionary<string, object> v, ProtType t, PD_Obj pd) : base(v, t, pd)
		{
			if (v.ContainsKey("str_type")) str_type = v["str_type"] as string;
			if (v.ContainsKey("st_pos")) st_pos = (int)v["st_pos"];
			if (v.ContainsKey("mode"))
			{
				string s = MC_Prot.json_ser.Serialize(v["mode"]); //这样取得的字符串带"
				mode = MC_Prot.json_ser.Deserialize<CheckMode>(s); //取得参数类型，enum类型的反串行化需要字符串带"
			}
			if(v.ContainsKey("fix"))
			{
				string s = v["fix"] as string;
				if (str_type == "hex")
				{
					fix = UInt64.Parse(s, System.Globalization.NumberStyles.HexNumber);
				}
				else UInt64.TryParse(s, out fix);
			}
			len = get_mode_len(mode);
			//更新输入结构
			v["len"] = len;
		}
		public override int pro(byte[] b, ref int off, int n) //在二进制流中取得字符
		{ //注意n是off之后还有多少。存储一定是头部存在0偏移的
			if (len > n) return 1; //若输入数量不够，不解，返回未完成，下次从新解
			int pre_off = off; //off会被修改，这里缓存，作为此域的偏移（前边的长度）
			int i = data.set_val(b, off, len); //按字节为本类型的数据赋值
			off += i + skip_n; //本协议域解析完成后需要往后加多少字节（改为结构体输入后废弃，以后用skip类型）
			switch (mode)
			{
				case CheckMode.fix8: if (data.du8 != fix) return 2; break;
				case CheckMode.fix16: if (data.du16 != fix) return 2; break;
				case CheckMode.fix32: if (data.du32 != fix) return 2; break;
				case CheckMode.fix64: if (data.du64 != fix) return 2; break;
				case CheckMode.sum8:
					break;
				case CheckMode.sum16:
					break;
				case CheckMode.crc16:
					{
						UInt16 crc = Tool.crc_ccitt(b, st_pos, pre_off);
						if (crc != data.du16) return 2;
					}
					break;
				case CheckMode.mdcrc16:
					break;
				case CheckMode.crc32:
					break;
				default:
					break;
			}
			return 0;
		}
		public static int get_mode_len(CheckMode m) //获得不同模式的长度
		{
			switch (m)
			{
				case CheckMode.fix8:
				case CheckMode.sum8:
					return 1;
				case CheckMode.fix16:
				case CheckMode.sum16:
				case CheckMode.crc16:
				case CheckMode.mdcrc16:
					return 2;
				case CheckMode.fix32:
				case CheckMode.crc32:
					return 4;
				case CheckMode.fix64:
					return 8;
				default: return 0;
			}
		}
	}
}

