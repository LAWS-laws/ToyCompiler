

using ToyCompiler.Data;

namespace ToyCompiler.Compiler;


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