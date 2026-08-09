using System.Text;
using UnityEngine;

/// <summary>
/// BlendShape 名稱解析。FBX 匯進來的通道名可能長成 "(whithballshape)small"，
/// 所以先找完全相符，再忽略大小寫與符號比對一次，最後才用包含關係。
/// </summary>
public static class MooncakeBlendShapeUtil
{
    public static int ResolveIndex(SkinnedMeshRenderer skinned, string shapeName, int indexOverride = -1)
    {
        if (skinned == null || skinned.sharedMesh == null) return -1;

        var mesh = skinned.sharedMesh;
        if (indexOverride >= 0)
            return indexOverride < mesh.blendShapeCount ? indexOverride : -1;

        int exact = mesh.GetBlendShapeIndex(shapeName);
        if (exact >= 0) return exact;

        string wanted = Simplify(shapeName);
        if (wanted.Length == 0) return -1;

        int contains = -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string candidate = Simplify(mesh.GetBlendShapeName(i));
            if (candidate == wanted) return i;
            if (contains < 0 && candidate.Contains(wanted)) contains = i;
        }

        return contains;
    }

    public static string Simplify(string source)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;

        var sb = new StringBuilder(source.Length);
        foreach (char c in source)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
