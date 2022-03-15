using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace cslib
{
	public static class Tool
	{
		//struct转换为byte[]
		public static byte[] StructToBytes(object structObj)
		{
			int size = Marshal.SizeOf(structObj);
			IntPtr buffer = Marshal.AllocHGlobal(size);
			try
			{
				Marshal.StructureToPtr(structObj, buffer, false);
				byte[] bytes = new byte[size];
				Marshal.Copy(buffer, bytes, 0, size);
				return bytes;
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}
		//byte[]转换为struct
		public static object BytesToStruct(byte[] bytes, Type strcutType)
		{
			int size = Marshal.SizeOf(strcutType);
			IntPtr buffer = Marshal.AllocHGlobal(size);
			try
			{
				Marshal.Copy(bytes, 0, buffer, size);
				return Marshal.PtrToStructure(buffer, strcutType);
			}
			catch
			{
				return null;
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}
		//为方便结构体数组的构造
		public static object BytesToStruct(byte[] bytes, int offset, Type strcutType)
		{
			int size = Marshal.SizeOf(strcutType);
			IntPtr buffer = Marshal.AllocHGlobal(size);
			try
			{
				Marshal.Copy(bytes, offset, buffer, size);
				return Marshal.PtrToStructure(buffer, strcutType);
			}
			catch
			{
				return null;
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}
		//CRC(16位) = X16+X15+X2+1 　　CRC(CCITT) = X16+X12+X5+1
		//计算CRC16，0x11021
		public static UInt16[] crc_ccitt_tab = new UInt16[]
		{
			0x0000,0x1021,0x2042,0x3063,0x4084,0x50A5,0x60C6,0x70E7,
			0x8108,0x9129,0xA14A,0xB16B,0xC18C,0xD1AD,0xE1CE,0xF1EF,
			0x1231,0x0210,0x3273,0x2252,0x52B5,0x4294,0x72F7,0x62D6,
			0x9339,0x8318,0xB37B,0xA35A,0xD3BD,0xC39C,0xF3FF,0xE3DE,
			0x2462,0x3443,0x0420,0x1401,0x64E6,0x74C7,0x44A4,0x5485,
			0xA56A,0xB54B,0x8528,0x9509,0xE5EE,0xF5CF,0xC5AC,0xD58D,
			0x3653,0x2672,0x1611,0x0630,0x76D7,0x66F6,0x5695,0x46B4,
			0xB75B,0xA77A,0x9719,0x8738,0xF7DF,0xE7FE,0xD79D,0xC7BC,
			0x48C4,0x58E5,0x6886,0x78A7,0x0840,0x1861,0x2802,0x3823,
			0xC9CC,0xD9ED,0xE98E,0xF9AF,0x8948,0x9969,0xA90A,0xB92B,
			0x5AF5,0x4AD4,0x7AB7,0x6A96,0x1A71,0x0A50,0x3A33,0x2A12,
			0xDBFD,0xCBDC,0xFBBF,0xEB9E,0x9B79,0x8B58,0xBB3B,0xAB1A,
			0x6CA6,0x7C87,0x4CE4,0x5CC5,0x2C22,0x3C03,0x0C60,0x1C41,
			0xEDAE,0xFD8F,0xCDEC,0xDDCD,0xAD2A,0xBD0B,0x8D68,0x9D49,
			0x7E97,0x6EB6,0x5ED5,0x4EF4,0x3E13,0x2E32,0x1E51,0x0E70,
			0xFF9F,0xEFBE,0xDFDD,0xCFFC,0xBF1B,0xAF3A,0x9F59,0x8F78,
			0x9188,0x81A9,0xB1CA,0xA1EB,0xD10C,0xC12D,0xF14E,0xE16F,
			0x1080,0x00A1,0x30C2,0x20E3,0x5004,0x4025,0x7046,0x6067,
			0x83B9,0x9398,0xA3FB,0xB3DA,0xC33D,0xD31C,0xE37F,0xF35E,
			0x02B1,0x1290,0x22F3,0x32D2,0x4235,0x5214,0x6277,0x7256,
			0xB5EA,0xA5CB,0x95A8,0x8589,0xF56E,0xE54F,0xD52C,0xC50D,
			0x34E2,0x24C3,0x14A0,0x0481,0x7466,0x6447,0x5424,0x4405,
			0xA7DB,0xB7FA,0x8799,0x97B8,0xE75F,0xF77E,0xC71D,0xD73C,
			0x26D3,0x36F2,0x0691,0x16B0,0x6657,0x7676,0x4615,0x5634,
			0xD94C,0xC96D,0xF90E,0xE92F,0x99C8,0x89E9,0xB98A,0xA9AB,
			0x5844,0x4865,0x7806,0x6827,0x18C0,0x08E1,0x3882,0x28A3,
			0xCB7D,0xDB5C,0xEB3F,0xFB1E,0x8BF9,0x9BD8,0xABBB,0xBB9A,
			0x4A75,0x5A54,0x6A37,0x7A16,0x0AF1,0x1AD0,0x2AB3,0x3A92,
			0xFD2E,0xED0F,0xDD6C,0xCD4D,0xBDAA,0xAD8B,0x9DE8,0x8DC9,
			0x7C26,0x6C07,0x5C64,0x4C45,0x3CA2,0x2C83,0x1CE0,0x0CC1,
			0xEF1F,0xFF3E,0xCF5D,0xDF7C,0xAF9B,0xBFBA,0x8FD9,0x9FF8,
			0x6E17,0x7E36,0x4E55,0x5E74,0x2E93,0x3EB2,0x0ED1,0x1EF0
		};
		static public UInt16 crc_ccitt_start_value = 0; //初始值
		static public UInt16 crc_ccitt(byte[] p, int n)
		{
			UInt16 crc;
			int i;
			crc = crc_ccitt_start_value; //初始值
			for (i = 0; i < n; i++)
			{
				crc = (UInt16)((crc << 8) ^ crc_ccitt_tab[((crc >> 8) ^ p[i]) & 0x00FF]);
			}
			return crc;
		}
		static public uint[] Crc32Table = new uint[]
		{
			0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA,
			0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
			0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988,
			0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
			0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE,
			0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
			0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC,
			0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
			0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172,
			0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
			0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940,
			0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
			0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116,
			0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
			0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924,
			0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,

			0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A,
			0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
			0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818,
			0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
			0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E,
			0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
			0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C,
			0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
			0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2,
			0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
			0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0,
			0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
			0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086,
			0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
			0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4,
			0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,

			0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A,
			0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
			0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8,
			0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
			0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE,
			0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
			0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC,
			0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
			0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252,
			0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
			0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60,
			0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
			0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236,
			0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
			0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04,
			0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,

			0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A,
			0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
			0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38,
			0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
			0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E,
			0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
			0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C,
			0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
			0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2,
			0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
			0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0,
			0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
			0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6,
			0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
			0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94,
			0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D,
		};
		static public uint cal_crc32(byte[] p, int off, int n)
		{
			int i;
			uint dwCrc32 = 0xffffffff;
			for (i = 0; i < n; i++)
			{
				dwCrc32 = ((dwCrc32) >> 8) ^ Crc32Table[(p[i + off]) ^ ((dwCrc32) & 0x000000FF)];
			}
			return ~dwCrc32;
		}
		static public byte check_sum(byte[] p, int n)
		{
			byte acc = 0;
			int i;
			for (i = 0; i < n; i++) acc += p[i];
			return acc;
		}
		public static UInt16 ChangeEnd16(UInt16 n)
		{
			return (UInt16)((((UInt16)(n)) << 8) | (((UInt16)(n)) >> 8));
		}
		//A率压缩速度，分辨率0.025m/s，12bit分辨率，压缩为8bit
		//实际值 高7bit   A率 bin
		//2047   111 1111 127 111 1111
		//1023   011 1111 111 110 1111
		// 511   001 1111  95 101 1111
		// 255   000 1111  79 100 1111
		// 127   000 0111  63  11 1111
		//  63   000 0011  47  10 1111
		//  31   000 0001  31  01 1111
		static public float A13_decode(sbyte v, float k) //A率13折线解码，输入变换比例
		{
			if (v == -128) v = -127; //避开最小值，C#检查的太严
			int a = v > 0 ? 1 : -1;
			short d = 0, dl = 0; //高3bit和低4bit
			int reso; //分辨率
			v = Math.Abs(v);
			d = (short)((v >> 4) & 0x7); //高3bit
			dl = (short)(v & 0xf); //低4bit
			byte[] tab = new byte[] { 1, 1, 2, 4, 8, 16, 32, 64 }; //分辨率
			reso = tab[d]; //分辨率
			d = (short)(reso * dl + (d == 0 ? 0 : (reso * 16)) + reso / 2); //尾数乘以分辨率，加上基准，加上偏置
			return d * a * k;
		}
		static public sbyte A13_encode(float v, float k) //A率13折线编码，输入变换比例
		{
			int a = v >= 0 ? 1 : -1;
			short d = 0, dl = 0; //高3bit和低4bit
			v /= k;
			if (v < -2047) v = -2047;
			else if (v > 2047) v = 2047;
			d = Math.Abs((short)v); //换成定点数,绝对值
			if (d > 1023) { dl = (short)((d - 1024) / 64); d = 7; }
			else if (d > 511) { dl = (short)((d - 512) / 32); d = 6; }
			else if (d > 255) { dl = (short)((d - 256) / 16); d = 5; }
			else if (d > 127) { dl = (short)((d - 128) / 8); d = 4; }
			else if (d > 63) { dl = (short)((d - 64) / 4); d = 3; }
			else if (d > 31) { dl = (short)((d - 32) / 2); d = 2; }
			else { dl = d; d = 0; }
			d = (short)((d << 4) + dl);
			return (sbyte)(d * a);
		}
		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		static public T load_json_from_file<T>(string fn)
		{
			StreamReader sr = new StreamReader(fn);
			string sbuf = sr.ReadToEnd();
			//先把注释去了
			Regex r = new Regex("//.*");
			sbuf = r.Replace(sbuf, "");
			//反串行化
			var t = json_ser.Deserialize<T>(sbuf);
			sr.Close();
			return t;
		}
		static public void dictinary_update(ref object old, object v) //用新的值更新原来的字典
		{
			var objv = v as Dictionary<string, object>;
			var arrv = v as ArrayList;
			var objold=old as Dictionary<string, object>;
			var arrold = old as ArrayList;
			int type_v = 0; //0为值，1为对象，2为数组
			int type_old = 0; //0为值，1为对象，2为数组
			if (objv != null) type_v = 1;
			else if(arrv!=null) type_v = 2;
			if (objold != null) type_old = 1;
			else if(arrold != null) type_old = 2;

			if(type_v != type_old || type_v==0) //若类型不同，或者是值，直接覆盖
			{
				old = v; return;
			}
			if (type_v == 1) //若是对象
			{
				foreach (var item in objv)
				{
					if (!objold.ContainsKey(item.Key)) //若之前没有这个键(差集)
					{
						objold[item.Key] = item.Value;
						continue;
					}
					var t = objold[item.Key];
					//之前就有，需要更新（交集）
					dictinary_update(ref t, item.Value);
					objold[item.Key] = t;
				}
				return ;
			}
			if (type_v == 2) //若是数组
			{
				arrold.AddRange(arrv);
			}
		}
		static public uint get_UTC() //获取当前utc时间
		{
			return (uint)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
		}
		static public T json_get<T>(Dictionary<string, object> dict, string s, T dft) //获得对象，若不存在，则取默认值
		{
			if (dict.ContainsKey(s))
			{
				return (T)(dict[s]);
			}
			return dft;
		}
	}
	public class LogFile //存储二进制数据的文件，根据配置创建文件路径，每隔固定时间重新建立文件
	{
		public BinaryWriter sw = null; //判断null确定是否在记录
		public string fname_time_fmt = "yyyyMMdd_HHmmss"; //时间格式
		public string ts_fmt="HHmmss.fff	"; //用于时间戳的时间格式，空则为不写时间戳
		public bool is_stdout=true; //是否在stdout打印
		public DirectoryInfo basepath = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory+"/data/"); //记录文件基础路径
		public string prefix = ""; //前缀
		public string suffix = ".txt"; //后缀
		public uint restart_T = 60 * 5; //重新建立文件的周期(s)

		public uint pre_utc = 0;//文件创建时的utc秒数
		public string filename = ""; //记录文件的全路径（只读）
		public LogFile()
		{
		}
		public virtual void create()
		{
			//建立文件夹
			if (!Directory.Exists(basepath.FullName))
			{
				Directory.CreateDirectory(basepath.FullName);
			}
			close();
			//建立文件
			pre_utc = Tool.get_UTC();
			sw = new BinaryWriter(new FileStream(filename, FileMode.Append));
		}
		~LogFile()
		{
			if (sw != null && sw.BaseStream.Length <= 0) delete();
			close();
		}
		public virtual void close()
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
			uint t = Tool.get_UTC();
			uint t_h = (t / restart_T) * restart_T; //整数记录点
			if (pre_utc < t_h || sw == null) //到了整数时间或文件还没建立
			{
				DateTime cur_time = DateTime.Now;
				filename = basepath.FullName + "/" + prefix + cur_time.ToString(fname_time_fmt) + suffix;
				create();
			}
		}
		public void delete()//删除文件
		{
			sw.Close();
			File.Delete(filename);
		}
		//二进制写入
		public virtual void log_pass(byte[] b,int ind,int len) //原始二进制写入
		{
			if(sw==null) return ;
			sw.Write(b, ind, len); //写入数据
		}
		public virtual void log_pass(string s) //原始文本写入
		{
			var b = Encoding.UTF8.GetBytes(s);
			log_pass(b, 0, b.Length); //写入数据
			if(is_stdout) Console.Write(s);
			sw.Flush();
		}
		//带文件名更新写入
		public void write(byte[] b, int ind, int len)
		{
			update_file_name();
			log_pass(b,ind,len);
		}
		public void write(byte[] b)
		{
			write(b, 0, b.Length);
		}
		public void write(string s) //直接写入文本
		{
			update_file_name();
			log_pass(s);
		}
		//按行写入，带文件名更新
		public void log(string s) //按行写入文本
		{
			if(!s.EndsWith("\n")) s+="\n"; //带换行
			if(ts_fmt!="") //若要写时间戳
			{
				string tmp_time = DateTime.Now.ToString(ts_fmt); //时间戳
				s=tmp_time+s;
			}
			write(s);
		}
		static public void delete_poll(LogFile lf,int len) //周期删除一种日志，输入日志对象，以及最多个数
		{
			var files = lf.basepath.GetFiles("*"+lf.suffix);
			if(files.Length>len)
			{
				Array.Sort(files,(FileInfo a,FileInfo b) => a.Name.CompareTo(b.Name)); //名称就是时间，从旧到新的顺序
				files[0].Delete();
			}
		}
	}
	public class Socket_cfg //网络通信配置
	{
		public Socket_cfg()
		{
			ip = "0.0.0.0";
			port = 12345;
			rmt_ip = "127.0.0.1";
			rmt_port = 12346;
		}
		public string ip { set; get; }
		public ushort port { set; get; }
		public string rmt_ip { set; get; }
		public ushort rmt_port { get; set; } //对方的ip和端口
	}
	public abstract class DataSrc //数据源
	{
		static public JavaScriptSerializer json_ser = new JavaScriptSerializer();
		static public DataSrc factory(Dictionary<string, object> v,RX_CB cb) //简单工厂
		{
			string s = (string)v["type"];
			Type t = Type.GetType("cslib.DataSrc_" + s); //数据源类的命名规则
			var r = Activator.CreateInstance(t,cb) as DataSrc;
			r.fromDict(v); //初始化对象
			return r;
		}
		static public DataSrc cur_ds=null; //当前数据源
		static public List<DataSrc> dslist = new List<DataSrc>();

		public string name = ""; //数据源的名称，如果是串口，则为串口号
		public DataSrc(RX_CB cb)
		{
			rx_event = cb;
		}
		virtual public void fromDict(Dictionary<string, object> v) //从配置加载数据源对象
		{
			if(v.ContainsKey("name")) name = (string)v["name"];
		}
		public delegate void RX_CB(byte[] b);
		public delegate void EVENT_CB(); //事件回调
		public RX_CB rx_event; //串口接收事件
		abstract public void open(string s); //打开数据源，输入以什么名称打开的
		virtual public void close()
		{ }
		abstract public string[] get_names(); //获取本数据源的名称，串口号等
		virtual public void send_data(byte[] b) //向设备发送数据
		{ }
	}
	public class DataSrc_udp : DataSrc //udp通信方式
	{
		const uint IOC_IN = 0x80000000;
		const uint IOC_VENDOR = 0x18000000;
		uint SIO_UDP_CONNRESET = (IOC_IN | IOC_VENDOR | 12); //windows必须设置一下否则出现远程主机强迫关闭了一个现有的连接
		public UdpClient udp = null; //接收数据转发
		public Socket_cfg cfg=new Socket_cfg(); //udp的配置，缓存，用于重连
		public bool is_reopen = false; //是否正在重连
		public bool is_open = false; //是否在开的状态，若关闭，也不用重连了
		public IPEndPoint rmt_addr = new IPEndPoint(0, 0); //接收到数据后，对方的地址
		public DataSrc_udp(RX_CB cb) : base(cb) { }
		public override void fromDict(Dictionary<string, object> v)
		{
			base.fromDict(v);
			cfg.ip = (string)v["ip"];
			int t = (int)v["port"];
			cfg.port = (ushort)t;
			if (v.ContainsKey("rmt_ip")) cfg.rmt_ip = (string)v["rmt_ip"];
			if (v.ContainsKey("rmt_port")) cfg.rmt_port = (ushort)(int)v["rmt_port"];
		}
		void udp_rx_cb(IAsyncResult ar) //接收失败后进行重连
		{
			IPEndPoint ipend = null;
			byte[] buf = null;
			try
			{
				buf = udp.EndReceive(ar, ref ipend);
			}
			catch (Exception e)
			{
				reopen(true);
				return;
			}
			try
			{
				if (buf != null)
				{
					rmt_addr = ipend;
					rx_event(buf);
				}
			}
			catch (Exception e) { } //用户的错不管
			try
			{
				udp.BeginReceive(udp_rx_cb, udp);
			}
			catch (Exception e)
			{
				reopen(true);
				return;
			}
		}
		public override void open(string s) //打开数据源，输入以什么名称打开的
		{
			udp = new UdpClient(new IPEndPoint(IPAddress.Parse(cfg.ip), cfg.port));
			udp.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
			//udp.Connect(new IPEndPoint(IPAddress.Parse(cfg.socket.rmt_ip), cfg.socket.rmt_port)); //若指定了目标，就收不到其他地址了
			udp.Client.ReceiveBufferSize = 1000000;
			is_open = true;
			udp.BeginReceive(udp_rx_cb, udp); //开始接收
		}
		public void reopen(bool block = false) //重连，输入是否阻塞
		{
			if (is_reopen || is_open==false) return; //正在重连，或者在关闭状态，这里就不管了
			is_reopen = true;
			do
			{
				Thread.Sleep(1000);
				try
				{
					close();
					open(name);
					break; //若有错，不会进回调
				}
				catch {	}
			} while (block);
			is_reopen = false;
		}
		public override void close()
		{
			is_open = false;
			if (udp!=null) udp.Close();
			udp = null;
		}
		public override string[] get_names()
		{
			return new string[] { name };
		}
		public void send_data(string ip, int port, byte[] b)
		{
			udp.Send(b, b.Length, ip, port);
		}
		public override void send_data(byte[] b)
		{
			send_data(cfg.rmt_ip,cfg.rmt_port, b);
		}
	}
	public class DataSrc_uart : DataSrc //串口方式
	{
		public SerialPort uart = new SerialPort(); //串口
		public int uart_b = 115200;
		public DataSrc_uart(RX_CB cb) : base(cb)
		{
			uart.DataReceived += (sender,e) =>
			{
				int n = 0;
				byte[] buf = new byte[0];
				try
				{
					n = uart.BytesToRead;
					buf = new byte[n];
					uart.Read(buf, 0, n);
				}
				catch
				{ }
				rx_event(buf);
			};
		}
		public override void fromDict(Dictionary<string, object> v)
		{
			base.fromDict(v);
			if (v.ContainsKey("uart_b")) uart_b = (int)v["uart_b"];
		}
		public override void open(string s) //打开数据源，输入以什么名称打开的
		{
			uart.PortName = s;
			uart.BaudRate = uart_b;
			uart.Open();
		}
		public override void close()
		{
			uart.Close();
		}
		public override string[] get_names()
		{
			return SerialPort.GetPortNames();
		}
		public override void send_data(byte[] b)
		{
			uart.Write(b, 0,b.Length);
		}
	}
}
