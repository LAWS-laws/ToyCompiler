using ToyCompiler.Data;

namespace ToyCompiler.Compiler;


/// <summary>
/// 全局符号。如函数，全局变量，类型
/// </summary>
public static class PublicTokens
{
	/// <summary>
	/// 公共变量数
	/// </summary>
	public static int PVarCount => Publicvars.Count;

	private readonly static (string, TypeID)[] typename =
	[
		("int",TypeID.Int),("float", TypeID.Float),("byte", TypeID.Byte),
		("bool", TypeID.Bool),("char",TypeID.Char),
	];// 所有类型的ID。可扩充
	private static List<Function> Funcs = [];
	private static List<IFunc> LibFuncs = [];
	private static List<RegVar> Publicvars = [];
	private static List<(string, int)> C_string = [];
	private static List<UserTypeDef> UserTypes = [];
	private static int Strlen = 0;//字符串常量段的长度(int32)
	private static TypeID id = TypeID.Bool + 1;

	/// <summary>
	/// 添加一个用户类型
	/// </summary>
	public static void Add_UserType(UserTypeDef typ)
	{
		for (int i = 0; i < UserTypes.Count; i++)
		{
			if (UserTypes[i].Name == typ.Name) throw new Exception("类型重名");
		}
		typ.Type.ID = id;//记录唯一的类型ID
		id++;
		UserTypes.Add(typ);
	}
	/// <summary>
	/// 记录所有用户函数和库函数的定义
	/// </summary>
	public static void Add_UserFunc(List<Function> funcs)
	{
		Funcs = funcs;
		LibFuncs = LibFunction.GetLibFuncs();
	}
	/// <summary>
	/// 记录用户的公共变量定义
	/// </summary>
	public static void Add_UserVar(RegVar vars)
	{
		for (int i = 0; i < Publicvars.Count; i++)
		{
			if (Publicvars[i].Name == vars.Name) throw new Exception("公共变量重名");
		}
		Publicvars.Add(vars);
	}
	/// <summary>
	/// 获取常量字符串的PZ头
	/// </summary>
	public static int Add_String(string str)
	{
		str += str.Length % 2 == 0 ? "\0\0" : '\0';//让长度为偶数，实现内存对齐

		for (int i = 0; i < C_string.Count; i++)
		{
			if (C_string[i].Item1 == str) return C_string[i].Item2;
		}

		int ret = Strlen + PVarCount;//偏移值加上全局变量区大小
		C_string.Add((str, ret));
		Strlen += str.Length / 2;
		return ret;
	}
	/// <summary>
	/// 根据ID返回内存布局信息。找不到会报错
	/// </summary>
	public static UserTypeDef GetUserType(TypeID id)
	{
		foreach (UserTypeDef ut in UserTypes)
		{
			if (ut.Type.ID == id) return ut;
		}
		throw new Exception("未知的用户类型");
	}
	/// <summary>
	/// 获取常量字符串区的内存映射
	/// </summary>
	public unsafe static int[] ConstString()
	{
		int[] ret = new int[Strlen];

		fixed (int* cp = ret)
		{
			char* p = (char*)cp;
			for (int i = 0; i < C_string.Count; i++)
			{
				for (int j = 0; j < C_string[i].Item1.Length; j++)
				{
					*p++ = C_string[i].Item1[j];
				}
			}
		}//遍历每个字符串，将其合并到int[]
		return ret;
	}
	/// <summary>
	/// 以名称获取公共变量。没找到返回null
	/// </summary>
	public static RegVar? GetVar(string name)
	{
		for (int i = 0; i < Publicvars.Count; i++)
		{
			if (Publicvars[i].Name == name) return Publicvars[i];
		}
		return null;
	}
	/// <summary>
	/// 寻找函数。找不到会报错
	/// </summary>
	public static IFunc GetFunction(string name, List<RegVar> typ)
	{
		for (int j = 0; j < 2; j++)
		{
			bool conv = j != 0;//第一次为false，第二次为true
			for (int i = 0; i < Funcs.Count; i++)
			{
				if (IsTargetFunc(Funcs[i], in name, typ, conv)) return Funcs[i];
			}
			for (int i = 0; i < LibFuncs.Count; i++)
			{
				if (IsTargetFunc(LibFuncs[i], in name, typ, conv)) return LibFuncs[i];
			}
		}
		throw new Exception("没有找到对应函数的重载");
	}
	/// <summary>
	/// 是否为类型标识符。如不是类型会返回Type.None
	/// </summary>
	public static TypeID GetTypeID(string str)
	{
		for (int i = 0; i < typename.Length; i++)
		{
			if (str == typename[i].Item1) return typename[i].Item2;
		}
		for (int i = 0; i < UserTypes.Count; i++)
		{
			if (UserTypes[i].Name == str) return UserTypes[i].Type.ID;
		}
		return TypeID.None;
	}
	/// <summary>
	/// 获得<see cref="Token"/>列表所表示的数据类型。不是类型会返回None
	/// </summary>
	public static VarType GetVarType(List<Token> tok, int start)
	{
		TypeID id = GetTypeID(tok[start].Str);
		int pdepth = 0;//记录类型名后的中括号数
		if (id != TypeID.None)
			for (start += 1; tok[start].Str == "[" && tok[start + 1].Str == "]"; start += 2) pdepth++;
		return new VarType(id, pdepth);
	}
	/// <summary>
	/// 类型的大小。如不是类型会报错
	/// </summary>
	public static int SizeOf(TypeID typ)
	{
		switch (typ)
		{
			case TypeID.Float:
			case TypeID.Int:
				return 4;
			case TypeID.Bool:
			case TypeID.Byte:
				return 1;
			case TypeID.Char:
				return 2;
			default:
				for (int i = 0; i < UserTypes.Count; i++)
				{
					if (UserTypes[i].Type.ID == typ) return 4;
				}
				throw new Exception("未知的类型大小");
		}
	}
	/// <summary>
	/// 源类型能否视作目标类型
	/// </summary>
	public static bool ConvertTo(VarType src, VarType dst)
	{
		if (dst.ID == src.ID) return true;
		if (src.Pdepth != 0 || dst.Pdepth != 0) return false;
		if (dst.ID == TypeID.Int)
		{
			if (src.ID == TypeID.Byte || src.ID == TypeID.Char) return true;
		}
		else if (dst.ID == TypeID.Char)
		{
			if (src.ID == TypeID.Byte) return true;
		}
		return false;
	}
	//====辅助函数==============
	/// <summary>
	/// 是否为匹配的重载函数。convert控制是否隐式转换
	/// </summary>
	private static bool IsTargetFunc(IFunc f, in string name, List<RegVar> typ, bool convert)
	{
		if (f.Name != name || f.Para.Count != typ.Count) return false;//同名，同参数长度
		for (int i = 0; i < f.Para.Count; i++)//逐参数对比
		{
			if (convert)//尝试隐式转换
			{
				if (!ConvertTo(typ[i].Type, f.Para[i])) return false;
			}
			else if (!f.Para[i].Equ(typ[i].Type)) return false;
		}
		return true;
	}
}