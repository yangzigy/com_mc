/*
文件名：main.h
创建时间：21:39 2011-1-28
版本：	V1.1			2019-01-15

功能：
定义全局宏
*/
#ifndef MAIN_H
#define MAIN_H

#include <stdio.h>
#include <time.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

#define GCC_VERSION (__GNUC__ * 10000 \
                     + __GNUC_MINOR__ * 100 \
                     + __GNUC_PATCHLEVEL__)
//#if GCC_VERSION > 30200 //GCC > 3.2.0
#if (GCC_VERSION >= 40902) || (_MSC_VER >= 1900) //4.9.2以上
#define USECPP11
#endif

#ifdef __cplusplus
#include <iostream>
#include <string>
#include <fstream>
#include <sstream>
#include <vector>
#include <algorithm>
#include <numeric>
#include <list>
#include <map>
#ifdef USECPP11 
#include <tuple>
#include <regex>
#include <random>
#include <chrono>
#include <thread>
#include <future>
#endif
using namespace std;
#endif

typedef unsigned char u8;
typedef unsigned short u16;
typedef unsigned int u32;
typedef unsigned long long u64;

typedef signed char s8;
typedef signed short s16;
typedef signed int s32;
typedef signed long long s64;

typedef volatile unsigned char vu8;
typedef volatile unsigned short vu16;
typedef volatile unsigned int vu32;
typedef volatile unsigned long long vu64;

typedef volatile signed char vs8;
typedef volatile signed short vs16;
typedef volatile signed int vs32;
typedef volatile signed long long vs64;

typedef unsigned long long t_maxu; //最大的整形
typedef signed long long t_maxs;

//定义精度误差
#define Eps	(1e-6)

#define TEST_ASSERT(x,y,exp)	cout<<endl;\
					if(exp)\
						cout<<__FILE__<<':'<<__LINE__<<":\033[32m passed\033[0m";\
					else \
					{\
						cout<<__FILE__<<':'<<__LINE__<<":\033[31m failed";\
						cout<<" exp:"<<(x)<<",ect:"<<(y);\
					} cout<<"\033[0m\t"
#define TEST_EQ(x,y)	TEST_ASSERT(x,y,(x)==(y))
#define TEST_FEQ(x,y)	TEST_ASSERT(x,y,fabs((x)-(y))<Eps) //浮点数的等于
#define TEST_NEAR(x,y,e)	TEST_ASSERT(x,y,fabs((x)-(y))<e)
#define TEST_LT(x,y)	TEST_ASSERT(x,y,(x)<(y))
#define TEST_GT(x,y)	TEST_ASSERT(x,y,(x)>(y))
#define TEST_LTEQ(x,y)	TEST_ASSERT(x,y,(x)<=(y))
#define TEST_GTEQ(x,y)	TEST_ASSERT(x,y,(x)>=(y))

#define CHECK_ASSERT(x,y,exp)	\
					if(exp){}\
					else \
					{\
						cout<<endl;\
						cout<<__FILE__<<':'<<__LINE__<<":\033[31m failed\033[0m";\
						cout<<" exp:"<<(x)<<",ect:"<<(y)<<"\033[0m\t";\
					}
#define CHECK_EQ(x,y)	CHECK_ASSERT(x,y,(x)==(y))
#define CHECK_FEQ(x,y)	CHECK_ASSERT(x,y,fabs((x)-(y))<Eps) //浮点数的等于
#define CHECK_NEAR(x,y,e)	CHECK_ASSERT(x,y,fabs((x)-(y))<(e))
#define CHECK_LT(x,y)	CHECK_ASSERT(x,y,(x)<(y))
#define CHECK_GT(x,y)	CHECK_ASSERT(x,y,(x)>(y))
#define CHECK_LTEQ(x,y)	CHECK_ASSERT(x,y,(x)<=(y))
#define CHECK_GTEQ(x,y)	CHECK_ASSERT(x,y,(x)>=(y))

//定义字节数量
#define KB(x)	(1024*(x))
#define MB(x)	(1024*1024ll*(x))
#define GB(x)	(1024*1024ll*1024*(x))

//大小端转换
#define CHANGE_END16(n) (u16)((((u16) (n)) << 8) | (((u16) (n)) >> 8))
#define CHANGE_END32(n)	(u32)( ((n)<<24) | (((n)&0xff00)<<8) | (((n)&0xff0000)>>8) | (((u32)(n))>>24) )
#define CHANGE_END64(n)	(u64)(\
				((n)<<56) | (((n)&0x000000000000ff00)<<40) | \
				(((n)&0x0000000000ff0000)<<24) | (((n)&0x00000000ff000000)<<8) | \
				(((n)&0x000000ff00000000)>>8) | (((n)&0x0000ff0000000000)>>24) | \
				(((n)&0x00ff000000000000)>>40) | (((n)&0xff00000000000000)>>56) )

//数值工具
#define MINMAX(d,min,max) if((d)<(min)) (d)=(min); else if((d)>(max)) (d)=(max);
#define ARRAY_SIZE(d) (sizeof(d)/sizeof(d[0])) //数组长度
#define PDBG	 printf("%s:%d\n",__FILE__,__LINE__)

#endif

