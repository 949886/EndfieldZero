using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace Cherry.Research;

public static class TechnologyTreeUtils
{
    public static TechnologyTreeDef LoadTreeResource(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
            return null;

        Resource loaded = ResourceLoader.Load(path);
        return ConvertResourceToTree(loaded);
    }

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

    private static TechnologyTreeDef ConvertResourceToTree(Resource resource)
    {
        if (resource == null)
            return null;

        if (resource is TechnologyTreeDef typedTree)
            return typedTree;

        var tree = new TechnologyTreeDef();
        foreach (Resource technologyResource in ReadResourceArray(resource, "Technologies"))
        {
            TechnologyDef technology = ConvertResourceToTechnology(technologyResource);
            if (technology != null)
                tree.Technologies.Add(technology);
        }

        NormalizeTree(tree);
        return tree;
    }

    private static TechnologyDef ConvertResourceToTechnology(Resource resource)
    {
        if (resource == null)
            return null;

        if (resource is TechnologyDef typedTechnology)
            return typedTechnology;

        var technology = new TechnologyDef
        {
            Id = ReadString(resource, "Id"),
            DisplayName = ReadString(resource, "DisplayName"),
            Description = ReadString(resource, "Description"),
            ResearchTicks = Math.Max(1, ReadInt(resource, "ResearchTicks", 300)),
            Icon = ReadResource<Texture2D>(resource, "Icon"),
            CanvasPosition = ReadVector2(resource, "CanvasPosition"),
        };

        foreach (string prereqId in ReadStringArray(resource, "PrerequisiteIds"))
            technology.PrerequisiteIds.Add(prereqId);

        foreach (Resource effectResource in ReadResourceArray(resource, "Effects"))
        {
            TechnologyEffectDef effect = ConvertResourceToEffect(effectResource);
            if (effect != null)
                technology.Effects.Add(effect);
        }

        return technology;
    }

    private static TechnologyEffectDef ConvertResourceToEffect(Resource resource)
    {
        if (resource == null)
            return null;

        if (resource is TechnologyEffectDef typedEffect)
            return typedEffect;

        return new TechnologyEffectDef
        {
            EffectType = ReadEnum(resource, "EffectType", TechnologyEffectType.UnlockBuilding),
            TargetId = ReadString(resource, "TargetId"),
            Value = ReadFloat(resource, "Value", 0f),
        };
    }

    private static IEnumerable<Resource> ReadResourceArray(Resource resource, string propertyName)
    {
        var values = new System.Collections.Generic.List<Resource>();
        if (resource == null)
            return values;

        try
        {
            Godot.Collections.Array array = (Godot.Collections.Array)resource.Get(propertyName);
            foreach (Variant entry in array)
            {
                if (entry.Obj is Resource child)
                    values.Add(child);
            }
        }
        catch
        {
        }

        return values;
    }

    private static IEnumerable<string> ReadStringArray(Resource resource, string propertyName)
    {
        var values = new System.Collections.Generic.List<string>();
        if (resource == null)
            return values;

        try
        {
            Godot.Collections.Array array = (Godot.Collections.Array)resource.Get(propertyName);
            foreach (Variant entry in array)
            {
                string value = entry.AsString();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }
        }
        catch
        {
        }

        return values;
    }

    private static string ReadString(Resource resource, string propertyName, string fallback = "")
    {
        try
        {
            return resource.Get(propertyName).AsString();
        }
        catch
        {
            return fallback;
        }
    }

    private static int ReadInt(Resource resource, string propertyName, int fallback)
    {
        try
        {
            return (int)resource.Get(propertyName);
        }
        catch
        {
            try
            {
                return (int)(long)resource.Get(propertyName);
            }
            catch
            {
                return fallback;
            }
        }
    }

    private static float ReadFloat(Resource resource, string propertyName, float fallback)
    {
        try
        {
            return (float)resource.Get(propertyName);
        }
        catch
        {
            try
            {
                return (float)(double)resource.Get(propertyName);
            }
            catch
            {
                return fallback;
            }
        }
    }

    private static Vector2 ReadVector2(Resource resource, string propertyName)
    {
        try
        {
            return (Vector2)resource.Get(propertyName);
        }
        catch
        {
            return Vector2.Zero;
        }
    }

    private static T ReadResource<T>(Resource resource, string propertyName) where T : Resource
    {
        try
        {
            return resource.Get(propertyName).Obj as T;
        }
        catch
        {
            return null;
        }
    }

    private static TEnum ReadEnum<TEnum>(Resource resource, string propertyName, TEnum fallback) where TEnum : struct, Enum
    {
        try
        {
            int raw = ReadInt(resource, propertyName, Convert.ToInt32(fallback));
            return (TEnum)Enum.ToObject(typeof(TEnum), raw);
        }
        catch
        {
            return fallback;
        }
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
