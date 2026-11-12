
using MsgPack = global::MessagePack;


#if !NETSTANDARD2_0 && !NETFRAMEWORK
[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
partial class MyResolver : MsgPack::IFormatterResolver
{
	public static readonly MyResolver Instance = new MyResolver();

	private static readonly MsgPack::Formatters.IMessagePackFormatter[] formatterList = new MsgPack::Formatters.IMessagePackFormatter[]
	{
	};

	private static readonly MsgPack::IFormatterResolver[] resolverList = new MsgPack::IFormatterResolver[]
	{
		global::MessagePack.Resolvers.NativeGuidResolver.Instance,
		new global::ResolverWithCtor(),
	};

	private MyResolver() { }

	public MsgPack::Formatters.IMessagePackFormatter<T> GetFormatter<T>()
	{
		return FormatterCache<T>.Formatter;
	}

#if !NETSTANDARD2_0 && !NETFRAMEWORK
	[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
	static class FormatterCache<T>
	{
		internal static readonly MsgPack::Formatters.IMessagePackFormatter<T> Formatter;

		static FormatterCache()
		{
			foreach (var formatter in formatterList)
			{
				if (formatter is MsgPack::Formatters.IMessagePackFormatter<T> f)
				{
					Formatter = f;
					return;
				}
			}

			foreach (var resolver in resolverList)
			{
				var f = resolver.GetFormatter<T>();
				if (f != null)
				{
					Formatter = f;
					return;
				}
			}
		}
	}
}

