using System.Text.RegularExpressions;

namespace ParadoxModTool.Compatch;

/// <summary>Namen der PMT-eigenen Patch-Dateien (<c>zz_pmt_compatch.txt</c>, <c>00_pmt_compatch.txt</c>, <c>zz_pmt_compatch_l_english.yml</c> …).</summary>
public static class OwnPatchFiles
{
    public static readonly Regex Pattern = new("^(0+|z+)_" + PatchPlanner.OwnFileName + "(\\.txt|\\.gui|_l_[a-z_]+\\.yml)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsOwn(string relativePath) => Pattern.IsMatch(Path.GetFileName(relativePath.Replace('\\', '/')));
}
