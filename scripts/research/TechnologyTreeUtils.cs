using System;
using Godot;
using Godot.Collections;

namespace Cherry.Research;

public static class TechnologyTreeUtils
{
    public static TechnologyTreeDef CreateEmptyTree()
    {
        return new TechnologyTreeDef();
    }

    public static TechnologyTreeDef CloneTree(TechnologyTreeDef source)
    {
        var clone = new TechnologyTreeDef();
        if (source == null)
            return clone;

        foreach (TechnologyDef technology in source.Technologies)
            clone.Technologies.Add(CloneTechnology(technology));

        return clone;
    }

    public static TechnologyDef CloneTechnology(TechnologyDef source)
    {
        if (source == null)
            return null;

        var clone = new TechnologyDef
        {
            Id = source.Id ?? "",
            DisplayName = source.DisplayName ?? "",
            Description = source.Description ?? "",
            ResearchTicks = Mathf.Max(1, source.ResearchTicks),
            Icon = source.Icon,
            CanvasPosition = source.CanvasPosition,
            PrerequisiteIds = new Array<string>(),
            Effects = new Array<TechnologyEffectDef>(),
        };

        foreach (string prereqId in source.PrerequisiteIds)
            clone.PrerequisiteIds.Add(prereqId);

        foreach (TechnologyEffectDef effect in source.Effects)
        {
            if (effect == null)
                continue;

            clone.Effects.Add(new TechnologyEffectDef
            {
                EffectType = effect.EffectType,
                TargetId = effect.TargetId ?? "",
                Value = effect.Value,
            });
        }

        return clone;
    }

    public static void NormalizeTree(TechnologyTreeDef tree)
    {
        if (tree == null)
            return;

        tree.Technologies ??= new Array<TechnologyDef>();

        for (int i = tree.Technologies.Count - 1; i >= 0; i--)
        {
            TechnologyDef tech = tree.Technologies[i];
            if (tech == null)
            {
                tree.Technologies.RemoveAt(i);
                continue;
            }

            tech.Id ??= "";
            tech.DisplayName ??= "";
            tech.Description ??= "";
            tech.ResearchTicks = Mathf.Max(1, tech.ResearchTicks);
            tech.PrerequisiteIds ??= new Array<string>();
            tech.Effects ??= new Array<TechnologyEffectDef>();

            for (int effectIndex = tech.Effects.Count - 1; effectIndex >= 0; effectIndex--)
            {
                TechnologyEffectDef effect = tech.Effects[effectIndex];
                if (effect == null)
                {
                    tech.Effects.RemoveAt(effectIndex);
                    continue;
                }

                effect.TargetId ??= "";
            }
        }
    }

    public static Texture2D GetDisplayIcon(TechnologyDef technology)
    {
        if (technology?.Icon != null)
            return technology.Icon;

        return BuildFallbackIcon(technology?.Id ?? technology?.DisplayName ?? "technology");
    }

    private static Texture2D BuildFallbackIcon(string seed)
    {
        int hash = Math.Abs(seed.GetHashCode());
        float hue = (hash % 360) / 360f;
        Color baseColor = Color.FromHsv(hue, 0.48f, 0.86f);
        Color dark = baseColor.Darkened(0.22f);
        Color light = baseColor.Lightened(0.18f);

        var image = Image.CreateEmpty(72, 72, false, Image.Format.Rgba8);
        image.Fill(new Color(0.08f, 0.09f, 0.12f, 1f));
        image.FillRect(new Rect2I(4, 4, 64, 64), dark);
        image.FillRect(new Rect2I(10, 10, 52, 52), baseColor);
        image.FillRect(new Rect2I(10, 52, 52, 10), light);
        image.FillRect(new Rect2I(16, 16, 18, 18), light.Lightened(0.16f));
        return ImageTexture.CreateFromImage(image);
    }
}
