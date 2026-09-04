
using System.Runtime.InteropServices;

namespace CompilerToy;

/// <summary>
/// 用于执行的程序
/// </summary>
public struct Program(int stklen,int clen, Ins[] ilist,int varcount, int[]constdata)
{
	public int PvarCount = varcount;
	public int StackLen = stklen;   //栈深度
	public int CallStackLen = clen; //调用栈深度
	public Ins[] InsList = ilist;   //指令表
	public int[] ConstDat = constdata;
}
/// <summary>
///	寄存器组
/// </summary>
public unsafe struct Regs
{
	public int R00; public int R01; public int R02; public int R03; public int R04; 
	public int R05; public int R06; public int R07; public int R08; public int R09; 
	public int R10; public int R11; public int R12; public int R13; public int R14; 
	public int R15; public int R16; public int R17; public int R18; public int R19; 
	public int R20; public int R21;
}

public class VirtualMachine
{
	private readonly Program Prog;
	public VirtualMachine(Program prog)
	{
		Prog = prog;
	}
	public unsafe void Run()
	{
		/*R0-R19 参数 R20 返回值地址 R21 存栈 22个*/
		Ins[] ilist = Prog.InsList;
		int[] stack = new int[Prog.StackLen];
		Regs[] Rgroups = new Regs[Prog.CallStackLen];
		Regs Rgroup = new Regs();
		int* publiczone = (int*)Marshal.AllocHGlobal((Prog.PvarCount + Prog.ConstDat.Length) * 4);
		int calldepth = 0;			//调用深度
		int* reg = (int*)&Rgroup;		//当前函数的寄存器组
		int pc = 0;					//程序计数器
		int sp = -1;                //栈顶指针

		Memset(publiczone, Prog.PvarCount);//初始化全局区
		Marshal.Copy(Prog.ConstDat, 0, (nint)(publiczone + Prog.PvarCount), Prog.ConstDat.Length);

		fixed (int* stack_p = stack)
		{
			while (true)
			{
				Ins ins = ilist[pc];
				switch (ins.ID)
				{
					case InsID.stop:
						goto DONE;
					case InsID.lod:
						reg[ins.Para1] = ins.Para2;
						break;
					case InsID.addi://addi r1 r2 r3 
						reg[ins.Para1] = reg[ins.Para2] + reg[ins.Para3];
						break;
					case InsID.subi:
						reg[ins.Para1] = reg[ins.Para2] - reg[ins.Para3];
						break;
					case InsID.muli:
						reg[ins.Para1] = reg[ins.Para2] * reg[ins.Para3];
						break;
					case InsID.divi:
						reg[ins.Para1] = reg[ins.Para2] / reg[ins.Para3];
						break;
					case InsID.modi:
						reg[ins.Para1] = reg[ins.Para2] % reg[ins.Para3];
						break;
					case InsID.gtri:
						reg[ins.Para1] = reg[ins.Para2] > reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.smri:
						reg[ins.Para1] = reg[ins.Para2] < reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.egtri:
						reg[ins.Para1] = reg[ins.Para2] >= reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.esmri:
						reg[ins.Para1] = reg[ins.Para2] <= reg[ins.Para3] ? 1 : 0;
						break;

					case InsID.addf:
						*(float*)&reg[ins.Para1] = *(float*)&reg[ins.Para2] + *(float*)&reg[ins.Para3];
						break;
					case InsID.subf:
						*(float*)&reg[ins.Para1] = *(float*)&reg[ins.Para2] - *(float*)&reg[ins.Para3];
						break;
					case InsID.mulf:
						*(float*)&reg[ins.Para1] = *(float*)&reg[ins.Para2] * *(float*)&reg[ins.Para3];
						break;
					case InsID.divf:
						*(float*)&reg[ins.Para1] = *(float*)&reg[ins.Para2] / *(float*)&reg[ins.Para3];
						break;
					case InsID.modf:
						*(float*)&reg[ins.Para1] = *(float*)&reg[ins.Para2] % *(float*)&reg[ins.Para3];
						break;
					case InsID.gtrf:
						reg[ins.Para1] = *(float*)&reg[ins.Para2] > *(float*)&reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.smrf:
						reg[ins.Para1] = *(float*)&reg[ins.Para2] < *(float*)&reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.egtrf:
						reg[ins.Para1] = *(float*)&reg[ins.Para2] >= *(float*)&reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.esmrf:
						reg[ins.Para1] = *(float*)&reg[ins.Para2] <= *(float*)&reg[ins.Para3] ? 1 : 0;
						break;

					case InsID.and:
						reg[ins.Para1] = reg[ins.Para2] != 0 && reg[ins.Para3] != 0 ? 1 : 0;
						break;
					case InsID.or:
						reg[ins.Para1] = reg[ins.Para2] != 0 || reg[ins.Para3] != 0 ? 1 : 0;
						break;
					case InsID.cjmp://cjmp a b
						if (reg[ins.Para1] == 0) { pc = ins.Para2; goto SKIPPC; }
						break;
					case InsID.jump://jump a
						pc = ins.Para1;
						goto SKIPPC;
					case InsID.equ:
						reg[ins.Para1] = reg[ins.Para2] == reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.nqu:
						reg[ins.Para1] = reg[ins.Para2] != reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.set:
						reg[ins.Para1] = reg[ins.Para2];
						break;

					case InsID.push:
						stack[++sp] = reg[ins.Para1];
						break;
					case InsID.pop:
						reg[ins.Para1] = stack[sp--];
						break;
					case InsID.call:
						if (ins.Para1 > 0)
						{
							stack[++sp] = pc + 1;
							pc = ins.Para1;
							Rgroups[calldepth] = Rgroup;
							Rgroup = Rgroups[++calldepth];//为debug考虑，暂时全部拷贝
							goto SKIPPC;
						}   //用户函数
						else                //库函数
						{
							/*此时，全部参数已入栈。只需要pop，然后根据需要push*/
							switch ((LibFuncID)ins.Para1)
							{
								case LibFuncID.PrintI:
									Console.WriteLine(stack[sp--]); break;
								case LibFuncID.PrintL:
									Console.WriteLine(stack[sp--] != 0); break;
								case LibFuncID.PrintF:
									int iii = stack[sp--];
									Console.WriteLine(*(float*)&iii); break;
								case LibFuncID.PrintC:
									Console.WriteLine((char)stack[sp--]); break;
								case LibFuncID.PrintStr:
									string st = new string((char*)stack[sp--]);
									Console.WriteLine(st); break;
								case LibFuncID.InputI:
									stack[++sp] = Convert.ToInt32(Console.ReadLine()); break;
								case LibFuncID.InputF:
									float fff = Convert.ToSingle(Console.ReadLine());
									stack[++sp] = *(int*)&fff; break;
								default:
									throw new Exception("没有找到库函数");
							}
							break;
						} //库函数
					case InsID.ret:
						sp = reg[21];                   //set stack
						stack[++sp] = reg[ins.Para1];   //push ret
						pc = reg[20];                   //jump
														//reg -= 22;
						Rgroup = Rgroups[--calldepth];  //恢复寄存器组
						goto SKIPPC;
					case InsID.ret0:
						sp = reg[21];                   //set stack
						pc = reg[20];                   //jump
														//reg-=22
						Rgroup = Rgroups[--calldepth];  //恢复寄存器组
						goto SKIPPC;
					case InsID.stsp:
						reg[21] = sp;
						break;

					case InsID.setp4:
						((int*)reg[ins.Para1])[reg[ins.Para2]] = reg[ins.Para3];
						break;
					case InsID.getp4:
						reg[ins.Para3] = ((int*)reg[ins.Para1])[reg[ins.Para2]];
						break;
					case InsID.setp4c:
						((int*)reg[ins.Para1])[ins.Para2] = reg[ins.Para3];
						break;
					case InsID.getp4c:
						reg[ins.Para3] = ((int*)reg[ins.Para1])[ins.Para2];
						break;
					case InsID.setp2:
						((short*)reg[ins.Para1])[reg[ins.Para2]] = *(short*)&reg[ins.Para3];
						break;
					case InsID.getp2:
						reg[ins.Para3] = ((short*)reg[ins.Para1])[reg[ins.Para2]];
						break;
					case InsID.setp2c:
						((short*)reg[ins.Para1])[ins.Para2] = *(short*)&reg[ins.Para3];
						break;
					case InsID.getp2c:
						reg[ins.Para3] = ((short*)reg[ins.Para1])[ins.Para2];
						break;
					case InsID.setp1:
						((byte*)reg[ins.Para1])[reg[ins.Para2]] = *(byte*)&reg[ins.Para3];
						break;
					case InsID.getp1:
						reg[ins.Para3] = ((byte*)reg[ins.Para1])[reg[ins.Para2]];
						break;
					case InsID.setp1c:
						((byte*)reg[ins.Para1])[ins.Para2] = *(byte*)&reg[ins.Para3];
						break;
					case InsID.getp1c:
						reg[ins.Para3] = ((byte*)reg[ins.Para1])[ins.Para2];
						break;
					case InsID.setpz:
						/*setpz num R1*/
						publiczone[ins.Para1] = reg[ins.Para2];
						break;
					case InsID.getpz:
						reg[ins.Para2] = publiczone[ins.Para1];
						break;
					case InsID.getpzl:
						reg[ins.Para2] = (int)&publiczone[ins.Para1];
						break;
					case InsID.malloc:
						/*malloc r1 r2 num => r1 = malloc(r2*num)*/
						reg[ins.Para1] = (int)Marshal.AllocHGlobal(reg[ins.Para2] * ins.Para3);
						break;
					case InsID.salloc:
						/*salloc r1 r2 r3 => r1 = stackalloc r2*r3*/
						int size = (int)MathF.Ceiling(reg[ins.Para2] * ins.Para3 / 4.0f);//4的倍数
						reg[ins.Para1] = (int)&stack_p[sp + 1];
						sp += size;//4的倍数
						break;

					case InsID.i2b:
						reg[ins.Para1] = (byte)reg[ins.Para2];
						break;
					case InsID.i2s:
						reg[ins.Para1] = (short)reg[ins.Para2];
						break;
					case InsID.i2f:
						*(float*)&reg[ins.Para1] = reg[ins.Para2];
						break;
					case InsID.f2i:
						reg[ins.Para1] = (int)*(float*)&reg[ins.Para2];
						break;
					default:
						throw new Exception("未知的指令");
				}
				//switch结束
				pc++;
			SKIPPC:;
			}
		//虚拟机运行结束
		DONE:;
		}
	}
	private unsafe static void Memset(int* p,int len)
	{
		int i = 0;
		while (i < len) p[i++] = 0;
	}
}