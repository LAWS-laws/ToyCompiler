using ToyCompiler.Data;
using ToyCompiler.Compiler;
using ToyCompiler.Preprocess;

namespace ToyCompiler;

public class CompilingProcess
{
	private readonly List<Line> Code;       //分成行的程序
	private readonly List<Function> Funcs;  //程序中的函数

	private ExpCompiler? Init_Sector;

	public CompilingProcess()
	{
		Code = [];
		Funcs = [];
		PublicTokens.Clear();
		PublicTokens.Add_UserFunc(Funcs);
	}
	/// <summary>
	/// 预处理代码。转语法糖，生成标准行
	/// </summary>
	public void PreProcess(string sourcecode)
	{
		DateTime dateTime = DateTime.Now;

		Code.AddRange(SynaxAnalyzer.Split2(sourcecode));
		Console.WriteLine("预处理用时：" + (DateTime.Now - dateTime).TotalMilliseconds + " 毫秒");
	}
	/// <summary>
	/// 编译生成变量，指令，常量池
	/// </summary>
	public void Compile()
	{/*已经处理了小括号*/

		DateTime dateTime = DateTime.Now;
		int depth = 0;
		int start = 0;//缓存函数开头行
		bool isclass = false;
		List<Line> publicvars = [];
		List<(int, int)> fregion = [];//缓存函数范围
		List<UserTypeDef> usertypes = [];

		for (int i = 0; i < Code.Count; i++)
		{
			if (Code[i].Tok[0].Str == "{") depth++;
			else if (Code[i].Tok[0].Str == "}")
			{
				depth--;
				if (depth < 0) { throw new Exception("正大括号数量少于反大括号"); }
				else if (depth == 0)
				{
					if (!isclass) fregion.Add((start, i));
					else usertypes.Add(new UserTypeDef(Code[start].Tok, start + 2, i - 1));

					start = i + 1;
				}
			}
			else if (depth == 0)
			{
				if (Code[i].Tok[0].Str == "class") { start = i; isclass = true; continue; }

				isclass = false;
				int ll = 1;
				while (Code[i].Tok[ll].Str == "[") ll += 2;
				ll++;
				if (ll < Code[i].Tok.Count && Code[i].Tok[ll].Str == "(") start = i;//函数
				else publicvars.Add(Code[i]);//变量
			}
		}//分出函数，用户类型与全局变量
		if (depth != 0) throw new Exception("大括号数量不匹配");

		for (int i = 0; i < usertypes.Count; i++)
		{
			PublicTokens.Add_UserType(usertypes[i]);
		}//先注册用户类型，以应对类型互相引用的情况
		for (int i = 0; i < usertypes.Count; i++)
		{
			usertypes[i].Decode(Code);
		}//后编译用户类型
		Console.WriteLine("全局变量表");
		for (int i = 0; i < publicvars.Count; i++)
		{
			VarType vt = PublicTokens.GetVarType(publicvars[i].Tok, 0);
			string nam = publicvars[i].Tok[1 + vt.Pdepth * 2].Str;
			PublicTokens.Add_UserVar(new RegVar(nam, vt, RegStat.Locked, i));
			Console.WriteLine("\t" + vt + "\t" + nam + "\t[" + i + ']');

			publicvars[i] = new Line(publicvars[i].Number,
								   publicvars[i].Tok[(1 + vt.Pdepth * 2)..],
								   publicvars[i].Pri[(1 + vt.Pdepth * 2)..]);
		}//添加全局变量定义
		GenInitSector(publicvars);//引导段
		Console.WriteLine();
		for (int i = 0; i < fregion.Count; i++)
		{
			Funcs.Add(new Function(Code[fregion[i].Item1].Tok, fregion[i].Item1 + 2, fregion[i].Item2 - 1));
			Funcs[^1].Compile(Code);
			Out(Funcs[^1]);
		}//遍历，定义并编译每个函数

		Console.WriteLine("编译用时：" + (DateTime.Now - dateTime).TotalMilliseconds + " 毫秒");
	}
	/// <summary>
	/// 生成引导代码段
	/// </summary>
	private void GenInitSector(List<Line> list)
	{
		Console.WriteLine(">> 变量初始化段");
		List<RegVar> RegVars = [];
		for (int i = 0; i < Config.MaxPara; i++)
		{
			RegVars.Add(new RegVar(string.Empty, new VarType(TypeID.None), RegStat.UnUsed, i));
		}//初始化寄存器表
		Init_Sector = new ExpCompiler(RegVars, new VarType(TypeID.None));
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].Tok.Count == 1) continue;
			Init_Sector.Compile(list[i].Tok, list[i].Pri, string.Empty);
		}

		Init_Sector.Ilist.Add(new Ins(InsID.call, 0, 0, 0));//调用main函数
		Init_Sector.Ilist.Add(new Ins(InsID.stop, 0, 0, 0));//从main返回后结束程序
		Log.Out();
	}
	/// <summary>
	/// 链接函数。确定跳转指令地址
	/// </summary>
	public CompiledProgram Link()
	{
		if (Init_Sector == null) throw new Exception();
		/*链接产物是指令表。调用深度，栈深度
		  先生成引导代码段，然后依序排列指令。*/
		DateTime tim = DateTime.Now;
		List<Ins> Ilist = [];
		int Ilistlen = Init_Sector.Ilist.Count;
		int clen = 20;
		int stacklen = 0;

		for (int i = 0; i < Funcs.Count; i++)
		{
			Funcs[i].Head = Ilistlen;
			Ilistlen += Funcs[i].Ilength;
			if (Funcs[i].Name == "Main")
			{
				Init_Sector.Ilist[^2] = new Ins(InsID.call, Funcs[i].Head, 0, 0);//设置main函数调用
				if (!Funcs[i].RetType.Equ(TypeID.None)) throw new Exception("Main函数不能有返回值");
				if (Funcs[i].Para.Count > 0) throw new Exception("Main函数应是无参数的");
			}
		}//仅设置函数地址
		Init_Sector.Link(0);
		Ilist.AddRange(Init_Sector.Ilist);//引导代码段

		if (Ilist[Init_Sector.Ilist.Count - 2].Para1 == 0) throw new Exception("没有找到Main函数");
		for (int i = 0; i < Funcs.Count; i++)
		{
			Ilist.AddRange(Funcs[i].Link());
			stacklen += Funcs[i].Para.Count;
		}//链接函数并把代码拷贝到指令表
		if (Ilist.Count == Init_Sector.Ilist.Count) throw new Exception("没有生成任何指令");
		stacklen *= 3;//栈深度以4字节计
		stacklen += 200;
		Console.WriteLine("链接用时：" + (DateTime.Now - tim).TotalMilliseconds + " 毫秒");

		return new CompiledProgram(stacklen, clen, Ilist.ToArray(), PublicTokens.PVarCount, PublicTokens.ConstString());
	}

	/// <summary>
	/// 用于debug
	/// </summary>
	private static void Out(Function f)
	{
		Console.Write("<Function> " + f.RetType.ToString() + ' ' + f.Name + '(');
		for (int i = 0; i < f.Para.Count; i++) Console.Write(f.Para[i] + " " + f.RegVars[i].Name + " ,");
		Console.WriteLine(')');
		Console.WriteLine("<寄存器表>");
		List<RegVar> Vtype = f.RegVars;
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].State == RegStat.UnUsed) continue;
			Console.WriteLine("\t" + Vtype[i].Type.ToString() + '\t' + Vtype[i].Name + "\t[" + Vtype[i].Offest + "]");
		}
		Console.WriteLine("<指令表>");
		Log.Out();
		Console.WriteLine();
	}
}
