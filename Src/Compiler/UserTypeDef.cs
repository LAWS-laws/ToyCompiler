using ToyCompiler.Data;

namespace ToyCompiler.Compiler;

/// <summary>
/// 用户自定义类型
/// </summary>
public class UserTypeDef
{
	public VarType Type;
	public readonly string Name;
	public int ActualSize; //int32
	public readonly bool IsClass;

	private readonly List<RegVar> Member = []; //偏移值是可用的立即数
	private readonly int Start, End;

	public UserTypeDef(List<Token> tok, int start, int end)
	{
		if (tok.Count != 2 || tok[1].Type != TokenType.Letter) throw new Exception("语法错误");
		if (tok[0].Str == "class") IsClass = true;
		Name = tok[1].Str;
		Start = start; End = end;
	}
	/// <summary>
	/// 根据名称返回成员信息。找不到会报错
	/// </summary>
	public RegVar GetMember(string name)
	{
		for (int i = 0; i < Member.Count; i++)
		{
			if (Member[i].Name == name) return Member[i];
		}
		throw new Exception(Name + "未包含" + name + "的定义");
	}
	/// <summary>
	/// 解析类成员
	/// </summary>
	public void Decode(List<Line> line)
	{/*已经得到了ID*/
		int bytes = 0;//总字节数
		for (int i = Start; i <= End; i++)
		{
			VarType vt = PublicTokens.GetVarType(line[i].Tok, 0);
			if (vt.ID == TypeID.None) throw new Exception("未知的类型：" + line[i].Tok[0].Str);
			string name = line[i].Tok[1 + vt.Pdepth * 2].Str;
			int size = vt.Pdepth != 0 ? 4 : PublicTokens.SizeOf(vt.ID);//该成员所占字节数
			int offest = (int)Math.Ceiling((float)bytes / size);
			bytes = (offest + 1) * size;

			Member.Add(new(name, vt, RegStat.Locked, offest));
		}
		ActualSize = (int)Math.Ceiling((float)bytes / 4);
	}
}