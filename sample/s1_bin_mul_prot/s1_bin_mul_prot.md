<link rel="stylesheet" type="text/css" href="../../doc/base.css">

s1_bin_mul_prot 二进制多协议域案例  （当前不可用）
======  
## 协议
二进制数据包分为3种类型：  
1. 状态包（共20Byte）：  

| 位置 | 长度 | 参数 |  
| -- | --  | -- |  
| 0 | 1B | 同步字0xaa |  
| 1 | 1B | 类型：1：状态包 |  
| 2 | 2B | 电压：单位0.1 |  
| 4 | 2B | 空 |  
| 6 | 2B | 电流：单位0.1 |  
| 8 | 2B | 强度：单位0.1 |  
| 10 | 2B | 期望温度：单位0.1 |  
| 12 | 2B | 温度：单位0.1 |  
| 14 | 1B | 空 |  
| 15 | 3B | 状态字 |  
| 18 | 2B | crc |  
2. 参数包1（共9Byte）：  

| 位置 | 长度 | 参数 |  
| -- | --  | -- |  
| 0 | 1B | 同步字0xaa |  
| 1 | 1B | 类型：2：参数包 |  
| 2 | 2~3B | 参数结构 |  
|  | 2~3B | 参数结构 |  
| 7 | 2B | crc |  
此包中包含2个参数结构，其中参数结构有两种：  
- 指令回复，第一字节为0x01，第二字节为回复值，0失败1成功  
- 电流偏置，第一字节为0x02，第二、三字节为电流值，单位0.1  

这两种结构先后顺序不做规定，但数据包中一定有这两种结构  
3. 参数包2（共9Byte）： 

与参数包1完全相同，只是同步头由aa改为了e2

示例数据：  
1. 状态包  
aa 01 00 ff 00 00 20 00 10 00 f0 00 00 01 00 b9 8d 20 7b c5  
2. 参数包1
aa 02 01 00 02 80 00 a8 62  
aa 02 02 45 00 01 01 4b 08  
3. 参数包2
e2 02 01 00 02 80 00 a8 62  
## 配置
根据以上协议，系统中共有两种同步头，所以有两个协议族，aa协议族和e2协议族，相应的，应配置2个协议根节点。
## 测试
测试数据为20230127_103344.cmlog，具有两个虚拟信道，0号为文本，1号为本案例中的二进制协议。
