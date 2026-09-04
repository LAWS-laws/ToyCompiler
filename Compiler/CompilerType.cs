using System.Text;

namespace CompilerToy;

/// <summary>
/// 寄存器状态
/// </summary>
public enum RegStat
{
	UnUsed,		//未使用
	Locked,		//用户变量，不可复用
	Occupied,	//可复用但被占
	Available,	//可立即复用
};
/// <summary>
/// 指令集
/// </summary>
public enum InsID
{
	stop,//stop

	addi, subi, muli, divi, modi,	//addi R1 R2 R3  R1=R2+R3
	gtri, smri,	egtri, esmri,		//gtri R1 R2 R3  R1=R2>R3
	
	addf, subf, mulf, divf, modf,
	gtrf, smrf, egtrf, esmrf,
	
	and,or,				//and R1 R2 R3  R1=R2&&R3
	//convert
	i2s,i2b,i2f,f2i,	//i2s R1 R2		R1 = (short)R2
	//common
	lod,                //lodi R1 num   R1 = num
	cjmp,				//cjmp R1 R2    if(!R1) goto R2;
	jump,				//jump R1       goto R1;
	equ,nqu,			//equ R1 R2 R3  R1=R2==R3
	set,				//set R1 R2     R1=R2
	push,				//push R1       R1 = *stack; stack--; 4byte
	pop,				//pop R1        stack++; *stack=R1;   4byte
	call,				//call R1		push pc+1; junp R1;
	ret,				//ret R1        setstack; push c; jump R20
	ret0,               //ret0			setstack; jump R20
	stsp,				//stsp			R21 = sp
	//pointer
	getpzl,             //getpzl num R1	R1 = &publiczone[num];
	setpz,getpz,		//setpz num R1	publiczone[num] = R1;
	setp4,getp4,		//setp4 R1 R2 R3	*(R1+R2) = R3
	setp4c,getp4c,		//setp4c R1 num R3	*(R1+num) = R3
	setp2,getp2,		//getp4 R1 R2 R3	R3 = *(R1+R2
	setp2c,getp2c,		//getp4c R1	num R3	R3 = *(R1+num)
	setp1,getp1,
	setp1c,getp1c,
	malloc,salloc,		//malloc R1 R2 num  R1 = Malloc(R2*num)
}
/// <summary>
/// 类型名
/// </summary>
public enum TypeID
{
	None,
	Int,
	Float,
	Byte,
	Char,
	Bool,
};
/// <summary>
/// 跳转语句类型
/// </summary>
public enum BranchType
{
	None,
	If,
	While,
}
/// <summary>
/// 符号类型
/// </summary>
public enum TokenType
{
	Notes,
	Letter,
	Symbol,
	Digit,
};
/// <summary>
/// 跳转语句组
/// </summary>
public class BranchMark(BranchType mode)
{
	public BranchType Mode = mode;  //跳转的类型
	public int HeadStart = 0;		//语句头算式的起始位置
	public int HeadMark = -1;		//开头的语句位置
	public List<int> TailMark = [];	//语句块结尾的跳转指令位置
}
/// <summary>
/// 变量的类型
/// </summary>
public struct VarType
{
	public TypeID ID;
	public int Pdepth;
	public VarType(TypeID id)
	{
		ID = id;
		Pdepth = 0;
	}
	public VarType(TypeID id,int depth)
	{
		ID = id;
		Pdepth = depth;
	}

	/// <summary>
	/// 是否与指定的 <see cref="VarType"/> 对象相等
	/// </summary>
	public readonly bool Equ(VarType varType)
	{
		return ID == varType.ID && Pdepth == varType.Pdepth;
	}
	/// <summary>
	/// 是否与指定的 <see cref="VarType"/> 对象相等
	/// </summary>
	public readonly bool Equ(TypeID id)
	{
		return ID == id && Pdepth == 0;
	}

	public readonly override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		if (ID <= TypeID.Bool) sb.Append(ID.ToString());
		else sb.Append(PublicTokens.GetUserType(ID).Name);

		for (int i = 0; i < Pdepth; i++) sb.Append("[]");
		return sb.ToString();
	}
}
/// <summary>
/// 符号
/// </summary>
public struct Token(TokenType typ, string str)
{
	public TokenType Type = typ;
	public string Str = str;
	public override string ToString()
	{
		return "["+ Type.ToString()[0] + "]   " + Str;
	}
}
/// <summary>
/// 用于执行的命令
/// </summary>
public struct Ins(InsID id, int p1, int p2, int p3)
{
	public InsID ID = id;
	public int Para1 = p1;
	public int Para2 = p2;
	public int Para3 = p3;

	public override string ToString()
	{
		if (ID.ToString().StartsWith("lod"))
		{
			return " " + ID.ToString() + " R" + Para1 + " " + Para2 + " ";
		}
		return " " + ID.ToString() + " R" + Para1 + " R" + Para2 + " R" + Para3 + " ";
	}
}
/// <summary>
/// 一个标准行
/// </summary>
public struct Line
{
	public int Number;
	public List<Token> Tok;
	public List<int> Pri;

	public Line()
	{
		Number = 0;
		Tok = [];
		Pri = [];
	}
	public Line(int num, List<Token> tok)
	{
		Number = num;
		Tok = tok;
		Pri = [];
	}
	public Line(int num, List<Token> tok, List<int> pri)
	{
		Number = num; Tok = tok; Pri = pri;
	}
	
	public override readonly string ToString()
	{
		StringBuilder sb = new();
		sb.Append(Number);
		for(int i=0;i<Tok.Count;i++)
		{
			sb.Append('~');
			sb.Append(Tok[i].Str);
		}
		return sb.ToString();
	}
}
public struct Ret_TokenType(RegVar? var, VarType typ, bool isstatic)
{
	public RegVar? Var = var;
	public VarType Type = typ;
	public bool IsStatic = isstatic;
}
/// <summary>
/// 库函数ID。用于链接与调用
/// </summary>
public enum LibFuncID
{
	PrintI = -1,
	PrintL = -2,
	PrintF = -3,
	PrintC = -4,
	PrintStr = -5,

	InputI = -6,
	InputF = -7,
}
public class LibFunction : IFunc
{
	private static readonly List<IFunc> LibFuncs =
	[
		new LibFunction("Print",[new VarType(TypeID.Int)],new(TypeID.None),LibFuncID.PrintI),
		new LibFunction("Print",[new VarType(TypeID.Bool)],new(TypeID.None),LibFuncID.PrintL),
		new LibFunction("Print",[new VarType(TypeID.Float)],new(TypeID.None),LibFuncID.PrintF),
		new LibFunction("Print",[new VarType(TypeID.Char)],new(TypeID.None),LibFuncID.PrintC),
		new LibFunction("Print",[new VarType(TypeID.Char,1)],new(TypeID.None),LibFuncID.PrintStr),
		new LibFunction("Input",[],new VarType(TypeID.Int),LibFuncID.InputI),
		new LibFunction("Input",[],new VarType(TypeID.Float),LibFuncID.InputF),
	];

	public string Name { get; }
	public List<VarType> Para { get; }
	public VarType RetType { get; }
	public int Head { get; }

	private LibFunction(string name, List<VarType> para, VarType retType, LibFuncID head)
	{
		Name = name;
		Para = para;
		RetType = retType;
		Head = (int)head;
	}

	public static List<IFunc> GetLibFuncs()
	{
		return LibFuncs;
	}
}
/// <summary>
/// 提供操作符对象
/// </summary>
public abstract class Operators
{
	public static readonly Operators Add = new Op_Add();
	public static readonly Operators Sub = new Op_Sub();
	public static readonly Operators Mul = new Op_Mul();
	public static readonly Operators Div = new Op_Div();
	public static readonly Operators Mod = new Op_Mod();
	public static readonly Operators Gtr = new Op_Gtr();
	public static readonly Operators Egtr = new Op_Egtr();
	public static readonly Operators Smr = new Op_Smr();
	public static readonly Operators Esmr = new Op_Esmr();
	public static readonly Operators Equ = new Op_Equ();
	public static readonly Operators Nqu = new Op_Nqu();
	public static readonly Operators And = new Op_And();
	public static readonly Operators Or = new Op_Or();

	protected InsID id;

	/// <summary>
	/// 根据类型返回该操作的字节码指令。附报错
	/// </summary>
	public InsID GetIns(VarType type)
	{
		if (type.Pdepth != 0) throw new Exception("无法为指针分配运算符");
		if (id <= InsID.esmri)//+ - * / % > < >= <=
		{
			if (type.ID == TypeID.Bool) throw new Exception("类型：" + type + "无法匹配指令");
			int off = type.ID == TypeID.Float? 1 : 0;
			return id + (InsID.addf - InsID.addi) * off;
		}
		else if (id <= InsID.or)//&& ||
		{
			if (type.ID != TypeID.Bool) throw new Exception("类型：" + type + "无法匹配指令");
			return id;
		}
		//== !=
		return id;
	}

	/// <summary>
	/// 执行该运算符的常量优化，附报错
	/// </summary>
	public abstract string Optimize(string left, string right);
	/// <summary>
	/// 返回运算结果的类型
	/// </summary>
	public TypeID GetRes(VarType typ)
	{
		if (typ.Pdepth != 0) throw new Exception("无法为指针分配运算符");

		if(id>InsID.modi) return TypeID.Bool;

		switch(typ.ID)
		{
			case TypeID.Int:
			case TypeID.Char:
			case TypeID.Byte:
				return TypeID.Int;
			case TypeID.Float:
				return TypeID.Float;
			default:throw new Exception("没有匹配的返回类型");
		}
	}
}
#region 操作符
public class Op_Add : Operators
{
	public Op_Add() { id = InsID.addi; }
	public override string Optimize(string left, string right)
	{
		return (Convert.ToSingle(left) + Convert.ToSingle(right)).ToString();
	}
	public override string ToString() => "+";
}//+
public class Op_Sub : Operators
{
	public Op_Sub() { id = InsID.subi; }
	public override string Optimize(string left, string right)
	{
		return (Convert.ToSingle(left) - Convert.ToSingle(right)).ToString();
	}
	public override string ToString() => "-";
}//-
public class Op_Mul : Operators
{
	public Op_Mul() { id = InsID.muli; }
	public override string Optimize(string left, string right)
	{
		return (Convert.ToSingle(left) * Convert.ToSingle(right)).ToString();
	}
	public override string ToString() => "*";
}//*
public class Op_Div : Operators
{
	public Op_Div() { id = InsID.divi; }
	public override string Optimize(string left, string right)
	{
		return (Convert.ToSingle(left) / Convert.ToSingle(right)).ToString();
	}
	public override string ToString() => "/";
}// /
public class Op_Mod : Operators
{
	public Op_Mod() { id = InsID.modi; }
	public override string Optimize(string left, string right)
	{
		return (Convert.ToSingle(left) % Convert.ToSingle(right)).ToString();
	}
	public override string ToString() => "%";
}// %
public class Op_Gtr : Operators
{
	public Op_Gtr() { id = InsID.gtri; }
	public override string Optimize(string left, string right)
	{
		return Convert.ToSingle(left) > Convert.ToSingle(right) ? "true" : "false";
	}
	public override string ToString() => ">";
}//>
public class Op_Egtr : Operators
{
	public Op_Egtr() { id = InsID.egtri; }
	public override string Optimize(string left, string right)
	{
		return Convert.ToSingle(left) >= Convert.ToSingle(right) ? "true" : "false";
	}
	public override string ToString() => ">=";
}//>=
public class Op_Smr : Operators
{
	public Op_Smr() { id = InsID.smri; }
	public override string Optimize(string left, string right)
	{
		return Convert.ToSingle(left) < Convert.ToSingle(right) ? "true" : "false";
	}
	public override string ToString() => "<";
}//<
public class Op_Esmr : Operators
{
	public Op_Esmr() { id = InsID.esmri; }
	public override string Optimize(string left, string right)
	{
		return Convert.ToSingle(left) <= Convert.ToSingle(right) ? "true" : "false";
	}
	public override string ToString() => "<=";
}//<=
public class Op_Equ : Operators
{
	public Op_Equ() { id = InsID.equ; }
	public override string Optimize(string left, string right)
	{
		return left == right ? "true" : "false";
	}
	public override string ToString() => "==";
}//==
public class Op_Nqu : Operators
{
	public Op_Nqu() { id = InsID.nqu; }
	public override string Optimize(string left, string right)
	{
		return left != right ? "true" : "false";
	}
	public override string ToString() => "!=";
}//!=
public class Op_And : Operators
{
	public Op_And() { id = InsID.and; }
	public override string Optimize(string left, string right)
	{
		return (left == "true" && right == "true") ? "true" : "false";
	}
	public override string ToString() => "&&";
}//&&
public class Op_Or : Operators
{
	public Op_Or() { id = InsID.or; }
	public override string Optimize(string left, string right)
	{
		return (left == "true" || right == "true") ? "true" : "false";
	}
	public override string ToString() => "||";
}//||
#endregion
