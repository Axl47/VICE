using S1API.Items;
using S1API.Items.Buildable;
using S1API.Products;
using UnityEngine;
using ItemCreator = S1API.Items.Storable.ItemCreator;
using BuildSoundType = S1API.Items.Buildable.BuildSoundType;

namespace PersonalMods.LSDGameplay;

internal static class Content
{
    internal static readonly CustomProductDefinition?[] Products = new CustomProductDefinition?[WorkshopRules.ArtSlots];
    private static readonly Dictionary<int, CustomProductDefinitionBuilder> Builders = new();
    private static ProductKind? Kind;
    private static ProductDefinition? Template;
    private static bool Initialized;

    internal static void Register()
    {
        StudioAssets.Initialize();
        Template ??= Il2CppScheduleOne.Registry.Instance.GetAllItems().ToArray()
            .Select(item=>ItemManager.GetDefinition(item.ID)).OfType<ShroomDefinition>().FirstOrDefault()
            ?? throw new InvalidOperationException("The native edible-product template is unavailable during pre-load.");
        Kind ??= new ProductKindBuilder(WorkshopRules.Owner + ":lsd").WithCompatibilityDrugType(DrugType.Shrooms).Build();
        if (!Initialized)
        {
            new ProductKindMetadataBuilder(Kind).WithDisplayName("LSD").WithColor(new(0.25f,0.78f,0.69f))
                .WithIcon(StudioAssets.Symbol).WithProductManagerVisibility().WithSearchAliases("tabs", "blotter").Build();
            foreach (var spec in new[] {
                (WorkshopRules.Prism,"Prism Base parcel", "Off Register specialist material. One parcel supports one forty-tab sheet.",30f),
                (WorkshopRules.Paper,"Blank blotter stock", "One sheet of paper for the Blotter Bench.",4f),
                (WorkshopRules.Ink,"Print cartridge", "A shared colour cartridge for one starter batch.",6f),
                (WorkshopRules.Sleeve,"Empty edition sleeve", "A folded sleeve for twenty matching tabs.",1f)
            })
                ItemCreator.CreateBuilder().WithBasicInfo(spec.Item1,spec.Item2,spec.Item3,ItemCategory.Ingredient)
                    .WithStackLimit(20).WithPricing(spec.Item4).WithIcon(StudioAssets.PropIcon(spec.Item1==WorkshopRules.Prism ? "Prism" : spec.Item1==WorkshopRules.Paper ? "Paper" : spec.Item1==WorkshopRules.Ink ? "Ink" : "Sleeve"))
                    .WithStoredItem(StudioAssets.Stored("Parcel")).WithoutStationItem().Build();
            FurnitureCreator.CreateBuilder().WithBasicInfo(WorkshopRules.Bench,"Blotter Bench","Manual print studio. Place inside the owned Sewer Office.")
                .WithModel(StudioAssets.Model("Bench")).WithFootprint(3,2).WithPricing(350).WithIcon(StudioAssets.PropIcon("Bench")).WithBuildSound(BuildSoundType.Metal).Build();
            FurnitureCreator.CreateBuilder().WithBasicInfo(WorkshopRules.Cabinet,"Blotter drying cabinet","One tray batch. Drying preserves quality. Place inside the owned Sewer Office.")
                .WithModel(StudioAssets.Model("Cabinet")).WithFootprint(2,2).WithPricing(250).WithIcon(StudioAssets.PropIcon("Cabinet")).WithBuildSound(BuildSoundType.Metal).Build();
            Initialized = true;
        }
        for (var slot=0;slot<WorkshopRules.ArtSlots;slot++)
        {
            if (Products[slot] is null) Products[slot]=Builder(slot).Build();
            ItemManager.EnsureItemRegistered(Products[slot]!);
            RegisterSheets(slot);
        }
        foreach (var id in new[] { WorkshopRules.Prism,WorkshopRules.Paper,WorkshopRules.Ink,WorkshopRules.Sleeve,WorkshopRules.Bench,WorkshopRules.Cabinet })
        {
            var definition=ItemManager.GetDefinition(id);
            if (definition is not null) ItemManager.EnsureItemRegistered(definition);
        }
    }

    internal static CustomProductDefinitionBuilder Builder(int slot)
    {
        if (slot is < 0 or >= WorkshopRules.ArtSlots) throw new InvalidDataException("Unknown studio edition slot.");
        if (Builders.TryGetValue(slot,out var builder)) return builder;
        var id=WorkshopRules.Product(slot);
        ProductPresentationProfileRegistry.RegisterForProduct(WorkshopRules.Owner,id,new ProductPresentationProfileBuilder()
            .WithLooseVisual(()=>StudioAssets.Visual("Tab",slot)).WithStoredVisual(()=>StudioAssets.Visual("Tab",slot))
            .WithHeldVisual(()=>StudioAssets.Visual("Tab",slot)).WithAvatarHeldVisual(()=>StudioAssets.Visual("Tab",slot))
            .WithStationVisual(()=>StudioAssets.Visual("Tab",slot)).WithFunctionalProductVisual(()=>StudioAssets.Visual("Tab",slot))
            .WithIcon(()=>StudioAssets.Icon(slot)).Build());
        var baggie=ProductPopulator.GetPackaging("baggie") ?? throw new InvalidOperationException("Native baggies are unavailable.");
        var jar=ProductPopulator.GetPackaging("jar") ?? throw new InvalidOperationException("Native jars are unavailable.");
        var brick=ProductPopulator.GetPackaging("brick") ?? throw new InvalidOperationException("Native bulk packaging is unavailable.");
        foreach (var packaging in new[] { baggie,jar })
        {
            var profile=new ProductPackagingContentProfileBuilder().WithContent(()=>StudioAssets.Visual("Tab",slot));
            for (var n=0;n<packaging.Quantity;n++) profile.AddPlacement(new ProductPresentationTransform(new(0,(n-packaging.Quantity/2f)*0.008f,0),new(0,n*11,0),Vector3.one));
            ProductPackagingContentProfileRegistry.Register(WorkshopRules.Owner,id,packaging.ID,profile.Build());
        }
        ProductPackagingContentProfileRegistry.Register(WorkshopRules.Owner,id,"brick",new ProductPackagingContentProfileBuilder()
            .WithCompleteFilledVisual(()=>StudioAssets.Visual("Packet",slot)).Build());
        builder=new CustomProductDefinitionBuilder(id,Kind!).WithName("LSD · "+WorkshopRules.EditionLabel(slot))
            .WithDescription("Prism formula. One tab is one sale unit. The studio edition is immutable.")
            .WithProductPrice(14).WithDefaultQuality(Quality.Standard).WithLegalStatus(LegalStatus.Illegal)
            .WithBaseAddictiveness(0.05f).WithEffectDurations(180,180).WithRepresentationsFrom(Template!)
            .WithValidPackaging(baggie,jar,brick).WithSaveProvider("personal.lsd:edition-v1",1,slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Builders[slot]=builder; return builder;
    }

    private static void RegisterSheets(int slot)
    {
        foreach (var dry in new[] { false,true }) for (var yield=36;yield<=40;yield++)
        {
            var id=WorkshopRules.Sheet(dry,slot,yield);
            var existing=ItemManager.GetDefinition(id);
            if (existing is not null) { ItemManager.EnsureItemRegistered(existing); continue; }
            ItemCreator.CreateBuilder().WithBasicInfo(id,$"{(dry ? "Ready" : "Wet")} sheet · {WorkshopRules.EditionLabel(slot)} ({yield} tabs)",
                dry ? "Return to the bench for cutting." : "Carry to the drying cabinet.",ItemCategory.Ingredient)
                .WithStackLimit(1).WithPricing(0).WithLegalStatus(LegalStatus.Illegal).WithIcon(StudioAssets.Icon(slot))
                .WithStoredItem(StudioAssets.Stored("Sheet",slot)).WithoutStationItem().Build();
        }
    }

    internal static Il2CppScheduleOne.ItemFramework.ItemInstance Create(string id,int quantity,string packaging="",int quality=2)
    {
        if (id.StartsWith(WorkshopRules.Owner+":tab-",StringComparison.Ordinal) && int.TryParse(id[(WorkshopRules.Owner.Length+5)..],out var slot)
            && slot is >= 0 and < WorkshopRules.ArtSlots && Products[slot] is { } product)
            return new Il2CppScheduleOne.Product.ProductItemInstance(Il2CppScheduleOne.Registry.GetItem(id),quantity,
                (Il2CppScheduleOne.ItemFramework.EQuality)quality,packaging.Length==0 ? null
                    : Il2CppScheduleOne.Registry.GetItem(packaging).TryCast<Il2CppScheduleOne.Product.Packaging.PackagingDefinition>());
        return Il2CppScheduleOne.Registry.GetItem(id).GetDefaultInstance(quantity).Cast<Il2CppScheduleOne.ItemFramework.ItemInstance>();
    }
}

public sealed class EditionSaveProvider : ICustomProductSaveProvider
{
    public string ProviderId=>"personal.lsd:edition-v1";
    public int MaximumDescriptorVersion=>1;
    public CustomProductDefinitionBuilder? Restore(CustomProductSaveDescriptor descriptor)
    {
        if (!int.TryParse(descriptor.ProviderData,out var slot) || slot is < 0 or >= WorkshopRules.ArtSlots || descriptor.ProductId!=WorkshopRules.Product(slot))
            return null;
        return Content.Builder(slot);
    }
}
