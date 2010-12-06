namespace ShinyConsole {
	public interface IConsoleCharacter {
		Font Font       { get; }
		char Glyph      { get; }
		uint Foreground { get; }
		uint Background { get; }
	}
}
