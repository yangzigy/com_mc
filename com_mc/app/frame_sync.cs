using System;
using System.Collections.Generic;
using System.Timers;
using System.Text;

namespace cslib
{
	/// <summary>
	/// 字符型数据的同步
	/// 此类默认的接收方式为，无起始字符，有终止字符（例如:\r\n）
	/// </summary>
	public class Line_Sync
	{
		public byte end_c = ((byte)'\n');
		public byte[] rec_buff=new byte[256];
		public int rec_p=0;//偏移指示
		/// <summary>
		/// 用户处理函数
		/// </summary>
		/// <param name="b"></param>
		/// <param name="len">为兼容C，需输入数组中有效数据长度</param>
		/// <returns>接收是否正确？不正确则不丢掉接收数据，继续扫描</returns>
		public virtual bool pro_pack(byte[] b, int len) { return true; }
		public void rec_byte(byte b)
		{
			rec_buff[rec_p++]=b;
			if(b== end_c)
			{
				rec_buff[rec_p]=0;
				try //若用户处理异常，不影响帧同步
				{
					pro_pack(rec_buff, rec_p); //结束，调用处理函数
				}
				catch { }
				rec_p=0;
			}
			if(rec_p>=rec_buff.Length-1)//为了兼容C，数组的最后一个字节要留0
			{
				rec_p=rec_buff.Length-2;
			}
		}
	}
	/// <summary>
	/// 带帧头的帧同步
	/// 注意：
	/// 	确定包长时，最小整包长度需大于等于pre_offset+2
	/// </summary>
	public class Frame_Sync
	{
		public byte[] SYNC={0xaa}; /// 同步字数组
		public int pack_len=2;
		public byte[] rec_buff=new byte[256];
		public int rec_p=0;//偏移指示
		/// <summary>
		/// 数据包中表示包长的字段所在位置
		/// 或者是数据包中表示数据包类型的字段所在位置
		/// 可作为可变包长数据包改变包长的信号
		/// </summary>
		public byte pre_offset=0;

		/// <summary>
		/// 用户改变包长的函数,len为当前接收到的长度
		/// 返回本数据包长度
		/// </summary>
		public virtual int pre_pack(byte[] b, int len) { return 0; }
		/// <summary>
		/// 用户处理数据包，返回是否正确，0正确
		/// </summary>
		public virtual int pro_pack(byte[] b, int len) { return 0; }
		public virtual void lostlock_cb(byte b){} //失锁回调
		public void rec_byte(byte b)
		{
			int pback=0; //回溯位置，在缓存中的偏移
			int l=0; //回溯长度
			while(true)
			{
				if(rec_p<SYNC.Length)//正在寻找包头
				{
					if(b==SYNC[rec_p])//引导字正确
					{
						rec_buff[rec_p++] = b;
					}
					else
					{
						lostlock_cb(b);
						rec_p=0;
					}
				}
				else if(rec_p==pre_offset)//可以改变包长
				{
					rec_buff[rec_p++]=b;
					pack_len= pre_pack(rec_buff, rec_p);
				}
				else//正常接收数据包
				{
					rec_buff[rec_p++]=b;
					if(rec_p>=pack_len)
					{
						int r=0;
						try //若用户处理异常，不影响帧同步
						{
							r= pro_pack(rec_buff, pack_len); //调用处理函数
						}
						catch{}
						if(r!=0) //若接收不正确
						{
							if(l==0) //若还没开始回溯
							{
								l= rec_p-1; //回溯长度,用rec_p可能大于pack_len
							}
							pback=1; //回溯位置
						}
						rec_p=0;
					}
				}
				if(l!=0) //若有回溯任务
				{
					b=rec_buff[pback];
					pback++; l--;
				}
				else return;
			}
		}
	}
	/// <summary>
	/// 带包头的组包,考虑位滑动
	/// 注意：
	/// 	确定包长时，最小整包长度需大于等于pre_offset+2
	/// </summary>
	public class Bit_Pack
	{
		public byte[] rec_buff;
		public byte[] SYNC;//同步字
		public int pack_len=0;//包全长
		public int pre_offset=0;//确定整包长度的位置
		public int pre_p=0;
		//位同步部分
		public int cur_sh=0;//数据右移位数
		public int pre_bit_p=0;//位同步进度
		public int SYNC_bit_len=0;
		public bool[] SYNC_bit;	//同步字位数组，为了方便判断，只能用bool型，int型对于非零不好判断
		public int[] bit_next;//kmp的next数组

		public virtual int pre_pack_len(byte[] b,int len){return pack_len;}
		public virtual bool pro_pack(byte[] b,int len){return true;}

		public Bit_Pack(int buf_n,byte[] syn)	//初始化
		{
			rec_buff=new byte[buf_n];
			SYNC=syn;
			SYNC_bit_len=SYNC.Length*8;
			SYNC_bit=new bool[SYNC_bit_len];
			bit_next=new int[SYNC_bit_len];
			int i;
			for(i=0;i<SYNC.Length;i++)
			{
				int j;
				for (j=0;j<8;j++)
				{
					SYNC_bit[i*8+j]=((SYNC[i] & (0x80>>j))!=0);
				}
			}
			//为KMP数组赋值
			for (i=0;i<SYNC_bit_len;i++)
			{
				bit_next[i]=0;
			}
			int k=0;
			for (i=1;i<SYNC_bit_len;i++)
			{
				while(k>0 && SYNC_bit[k]!=SYNC_bit[i])
				{
					k=bit_next[k-1];
				}
				if (SYNC_bit[k]==SYNC_bit[i])
				{
					k=k+1;
				}
				bit_next[i]=k;
			}
		}
		public int bit_sync(byte b)
		{
			int i;
			b<<=cur_sh;
			for (i=cur_sh;i<8;i++)
			{
				while(pre_bit_p>0 && ((b & 0x80)!=0) != SYNC_bit[pre_bit_p])
				{
					pre_bit_p=bit_next[pre_bit_p-1];
				}
				if (((b & 0x80)!=0) == SYNC_bit[pre_bit_p])
				{
					pre_bit_p++;
				}
				if (pre_bit_p>=SYNC_bit_len)
				{
					//完成了
					cur_sh=(i+1)%8;//确定序列右移值
					pre_bit_p=0;
					return 1;//找到了同步头
				}
				b<<=1;
			}
			return 0;
		}
		public void pack(byte[] b,int offset,int l)	//输入数据，偏移，长度
		{
			int p=offset;
			while(l>0)
			{
				if(pre_p<SYNC.Length)//正在寻找包头
				{
					int t=bit_sync(b[p]);
					if (t!=0)//若找到了包头
					{
						//memcpy(rec_buff,SYNC,syncbuf_len);
						Buffer.BlockCopy(SYNC, 0, rec_buff, 0, SYNC.Length);
						pre_p=SYNC.Length;	//为了退出寻找包头模式
						if(cur_sh!=0)
						{
							//若有位移，则在此就把剩余位用完
							rec_buff[pre_p]=(byte)(b[p]<<cur_sh);//末尾补零了
						}
					}
					l--;
					p++;
				}
				else if(pre_p<pre_offset)//确定不同包的长度
				{
					//可用数据：l
					//需要的数量：
					int need=pre_offset-pre_p;
					int r=need>l?l:need;//取其中比较小的那个
					if (cur_sh==0)//若没有位偏移，则直接复制
					{
						//memcpy(rec_buff+pre_p,p,r);
						Buffer.BlockCopy(b, p, rec_buff, pre_p, r);
						pre_p+=r;
						p+=r;
					}
					else
					{
						int i;
						for (i=0;i<r;i++)
						{
							rec_buff[pre_p] |= (byte)(b[p]>>(8-cur_sh));
							rec_buff[pre_p+1]=(byte)(b[p]<<cur_sh);//末尾补零了
							p++;
							pre_p++;
						}
					}
					l-=r;
					if (pre_p>=pre_offset)
					{
						pack_len=pre_pack_len(rec_buff, pre_p);
					}
				}
				else//正常接收数据包
				{
					//可用数据：l
					//需要的数量：
					int need=pack_len-pre_p;
					int r=need>l?l:need;//取其中比较小的那个
					//memcpy(rec_buff+pre_p,p,r);
					if (cur_sh==0)//若没有位偏移，则直接复制
					{
						//memcpy(rec_buff+pre_p,p,r);
						Buffer.BlockCopy(b, p, rec_buff, pre_p, r);
						pre_p+=r;
						p+=r;
					}
					else
					{
						int i;
						for (i=0;i<r;i++)
						{
							rec_buff[pre_p]|=(byte)(b[p]>>(8-cur_sh));
							rec_buff[pre_p+1]=(byte)(b[p]<<cur_sh);//末尾补零了
							p++;
							pre_p++;
						}
					}
					l-=r;
					if(pre_p>=pack_len)
					{
						//调用处理函数
						if(!pro_pack(rec_buff, pack_len))
						{//若接收不正确
							pre_p=0;
							if(cur_sh!=0)//若有位偏移，得把数组移回去再找
							{
								int i;
								for(i=pack_len-1;i>0;i--)
								{
									rec_buff[i]>>=cur_sh;
									rec_buff[i]|=(byte)((rec_buff[i-1])<<(8-cur_sh));
								}
								cur_sh=0;
								rec_buff[pack_len]=b[p-1];//把最后一个字节也加进来（移位回去的数据少半个字节，这样就全了）
								pack(rec_buff, 1, pack_len);//这里直接移动1字节，严格来说应该是一位，或根据KMP来确定
							}
							else
							{
								pack(rec_buff, 1, pack_len-1);//这里直接移动1字节，严格来说应该是一位，或根据KMP来确定
							}
						}
						else
						{
							pre_p=0;
							if (cur_sh!=0)//若有位偏移,要把没用完的输入用完
							{
								pack(b,p-1,1);
								//每次接收完成就应该置零
								cur_sh=0;//这东西只在复制数据时用一下
							}
						}
					}
				}
			}
		}
	}
}
