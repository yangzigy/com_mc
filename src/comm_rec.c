/*
文件名：comm_rec.c
时间：2013-9-15
版本：	V1.0

功能：

*/
#include "comm_rec.h"

void rec_sync(u8 b,COMM_SYNC* p)//同步方式接收函数
{
	p->rec_buff[p->rec_p++]=b;
	if(b==p->endc)
	{
		p->rec_buff[p->rec_p]=0;
		//结束，调用处理函数
		p->pro(p->rec_buff,p->rec_p);
		p->rec_p=0;
	}
	if(p->rec_p>=p->buf_len-1)//为了兼容C，数组的最后一个字节要留0
	{
		p->rec_p=p->buf_len-2;
	}
}

void rec_head(u8 b,COMM_HEAD* p)
{

	if(p->pre_p<p->syncbuf_len)//正在寻找包头
	{
		if(b==p->SYNC[p->pre_p])//引导字正确
		{
			p->rec_buff[p->pre_p++]=b;
		}
		else
		{
			p->pre_p=0;
		}
	}
	else if(p->pre_p==p->pre_offset)//确定不同包的长度
	{
		p->rec_buff[p->pre_p++]=b;
		p->pack_len=p->pre_cb(p->rec_buff,p-> pre_p);
	}
	else//正常接收数据包
	{
		p->rec_buff[p->pre_p++]=b;
		if(p->pre_p>=p->pack_len)
		{
			if(p->pack_len==p->pre_offset)
			{
				p->pack_len=p->pre_offset+1;
				p->pre_p=0;
				return;
			}
			//调用处理函数
			if(p->pro(p->rec_buff, p->pack_len))
			{//若接收不正确
				int i;
				int tem_len=p->pack_len;
				p->pre_p=0;
				i=p->syncbuf_len>0?p->syncbuf_len:1;
				for(;i<tem_len;i++)//从接收同步字后开始查找
				{
					rec_head(p->rec_buff[i], p);
				}
				return;
			}
			p->pre_p=0;
		}
	}
}

