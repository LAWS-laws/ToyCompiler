

using ToyCompiler.Data;

namespace ToyCompiler.Compiler;

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
			int off = type.ID == TypeID.Float ? 1 : 0;
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

		if (id > InsID.modi) return TypeID.Bool;

		switch (typ.ID)
		{
			case TypeID.Int:
			case TypeID.Char:
			case TypeID.Byte:
				return TypeID.Int;
			case TypeID.Float:
				return TypeID.Float;
			default: throw new Exception("没有匹配的返回类型");
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
