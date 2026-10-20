using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>Layout eines aufgelösten Fensters: Jomini (<see cref="GuiLayoutEngine"/>) oder klassisch (<c>ClassicLayoutEngine</c>).</summary>
public interface IGuiLayoutEngine
{
    LayoutBox Layout(ResolvedElement root, LayoutOptions? options = null);
}
