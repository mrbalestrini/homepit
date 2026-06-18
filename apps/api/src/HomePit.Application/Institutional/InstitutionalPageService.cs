using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Institutional;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;

namespace HomePit.Application.Institutional;

public sealed class InstitutionalPageService(
    IHomePitDbContext db,
    IUserContext userContext,
    IObjectStorage objectStorage,
    TimeProvider timeProvider)
{
    private const string PageSlug = "home";
    private const string SeoSlot = "seo";
    private const long ImageMaxBytes = 5 * 1024 * 1024;
    private const long SeoImageMaxBytes = 600 * 1024;
    private const int SeoImageWidth = 1200;
    private const int SeoImageHeight = 630;
    private const int MinimumListItems = 1;
    private const int MaximumListItems = 6;
    private static readonly byte[] RiffHeader = "RIFF"u8.ToArray();
    private static readonly byte[] WebpHeader = "WEBP"u8.ToArray();
    private static readonly byte[] Vp8Header = "VP8 "u8.ToArray();
    private static readonly byte[] Vp8LHeader = "VP8L"u8.ToArray();
    private static readonly byte[] Vp8XHeader = "VP8X"u8.ToArray();

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedSeoImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/webp"
    };

    public async Task<InstitutionalPageDto> GetPublicAsync(CancellationToken cancellationToken)
    {
        var page = await FindPageAsync(asTracking: false, cancellationToken);
        return page is null ? CreateDefaultDto() : ToDto(page);
    }

    public async Task<InstitutionalPageDto> GetAdminAsync(CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        return await GetPublicAsync(cancellationToken);
    }

    public async Task<InstitutionalPageDto> UpdateAsync(
        UpdateInstitutionalPageRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var normalized = ValidateAndNormalize(request);
        var page = await FindPageAsync(asTracking: true, cancellationToken);

        if (page is null)
        {
            page = new InstitutionalPage { Slug = PageSlug };
            db.InstitutionalPages.Add(page);
        }

        ApplyContent(page, normalized);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(page);
    }

    public async Task<InstitutionalPageDto> UploadImageAsync(
        string slot,
        Stream content,
        long contentLength,
        string? contentType,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var normalizedSlot = NormalizeSlot(slot);

        if (contentLength <= 0)
        {
            throw new ValidationException("Envie uma imagem com conteúdo para a página institucional.");
        }

        if (normalizedSlot == SeoSlot && contentLength > SeoImageMaxBytes)
        {
            throw new ValidationException("A imagem de SEO deve ter no máximo 600 KB.");
        }

        if (normalizedSlot != SeoSlot && contentLength > ImageMaxBytes)
        {
            throw new ValidationException("A imagem institucional deve ter no máximo 5 MB.");
        }

        var normalizedContentType = NormalizeImageContentType(normalizedSlot, contentType);
        var page = await GetOrCreatePageAsync(cancellationToken);
        var objectKey = ObjectStorageKeys.InstitutionalImage(PageSlug, normalizedSlot);

        if (normalizedSlot == SeoSlot)
        {
            var buffer = await ReadSeoImageBufferAsync(content, cancellationToken);
            EnsureSeoImageDimensions(buffer);

            await using var uploadStream = new MemoryStream(buffer, writable: false);
            await objectStorage.PutAsync(
                new ObjectStoragePutRequest(objectKey, uploadStream, buffer.Length, normalizedContentType),
                cancellationToken);
        }
        else
        {
            await objectStorage.PutAsync(
                new ObjectStoragePutRequest(objectKey, content, contentLength, normalizedContentType),
                cancellationToken);
        }

        ApplyImageMetadata(page, normalizedSlot, objectKey, normalizedContentType, timeProvider.GetUtcNow());

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(page);
    }

    public async Task<StoredObject> GetImageAsync(string slot, CancellationToken cancellationToken)
    {
        var normalizedSlot = NormalizeSlot(slot);
        var page = await db.InstitutionalPages
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Slug == PageSlug, cancellationToken)
            ?? throw new NotFoundException("Imagem institucional não encontrada.");

        var objectKey = normalizedSlot switch
        {
            "hero" => page.HeroImageObjectKey,
            "highlight" => page.HighlightImageObjectKey,
            SeoSlot => page.SeoImageObjectKey,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new NotFoundException("Imagem institucional não encontrada.");
        }

        return await objectStorage.GetAsync(objectKey, cancellationToken);
    }

    public async Task<InstitutionalPageDto> DeleteImageAsync(
        string slot,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var normalizedSlot = NormalizeSlot(slot);
        var page = await FindPageAsync(asTracking: true, cancellationToken)
            ?? throw new NotFoundException("Imagem institucional não encontrada.");

        var objectKey = normalizedSlot switch
        {
            "hero" => page.HeroImageObjectKey,
            "highlight" => page.HighlightImageObjectKey,
            SeoSlot => page.SeoImageObjectKey,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new NotFoundException("Imagem institucional não encontrada.");
        }

        await objectStorage.DeleteAsync(objectKey, cancellationToken);

        ClearImageMetadata(page, normalizedSlot);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(page);
    }

    private async Task<InstitutionalPage> GetOrCreatePageAsync(CancellationToken cancellationToken)
    {
        var page = await FindPageAsync(asTracking: true, cancellationToken);
        if (page is not null)
        {
            return page;
        }

        page = CreateDefaultEntity();
        db.InstitutionalPages.Add(page);
        await db.SaveChangesAsync(cancellationToken);
        return page;
    }

    private async Task<InstitutionalPage?> FindPageAsync(bool asTracking, CancellationToken cancellationToken)
    {
        var query = db.InstitutionalPages
            .Include(page => page.Benefits)
            .Include(page => page.Steps)
            .Where(page => page.Slug == PageSlug);

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private static InstitutionalPageDto ToDto(InstitutionalPage page)
    {
        return new InstitutionalPageDto(
            page.Slug,
            page.SeoTitle,
            page.SeoDescription,
            page.BrandName,
            page.BrandTagline,
            page.HeroEyebrow,
            page.HeroTitle,
            page.HeroDescription,
            page.PrimaryCtaLabel,
            page.PrimaryCtaUrl,
            page.BenefitsTitle,
            page.BenefitsDescription,
            page.Benefits
                .OrderBy(item => item.Position)
                .Select(item => new InstitutionalContentItemDto(item.Position, item.Title, item.Description))
                .ToArray(),
            page.StepsTitle,
            page.StepsDescription,
            page.Steps
                .OrderBy(item => item.Position)
                .Select(item => new InstitutionalContentItemDto(item.Position, item.Title, item.Description))
                .ToArray(),
            page.HighlightEyebrow,
            page.HighlightTitle,
            page.HighlightDescription,
            page.FinalCtaTitle,
            page.FinalCtaDescription,
            page.FooterText,
            page.HeroImageAlt,
            !string.IsNullOrWhiteSpace(page.HeroImageObjectKey),
            page.HeroImageUpdatedAt,
            page.HighlightImageAlt,
            !string.IsNullOrWhiteSpace(page.HighlightImageObjectKey),
            page.HighlightImageUpdatedAt,
            !string.IsNullOrWhiteSpace(page.SeoImageObjectKey),
            page.SeoImageUpdatedAt,
            page.UpdatedAt);
    }

    private static InstitutionalPageDto CreateDefaultDto() => ToDto(CreateDefaultEntity(), updatedAt: null);

    private static InstitutionalPageDto ToDto(InstitutionalPage page, DateTimeOffset? updatedAt)
    {
        var dto = ToDto(page);
        return dto with { UpdatedAt = updatedAt };
    }

    private static InstitutionalPage CreateDefaultEntity()
    {
        var page = new InstitutionalPage
        {
            Slug = PageSlug,
            SeoTitle = "HomePit | A base operacional da sua casa",
            SeoDescription = "Organize projetos, rotinas e conhecimento da casa em um ambiente compartilhado e feito para a vida real.",
            BrandName = "HomePit",
            BrandTagline = "Sua casa, organizada como um sistema vivo.",
            HeroEyebrow = "Organização residencial sem planilhas soltas",
            HeroTitle = "Transforme a rotina da casa em projetos que realmente avançam.",
            HeroDescription = "O HomePit reúne casas, universos, projetos, atividades e conhecimento reutilizável em uma experiência clara para toda a família.",
            PrimaryCtaLabel = "Falar com a HomePit",
            PrimaryCtaUrl = "https://homepit.example.com/contact",
            BenefitsTitle = "Uma base única para cuidar do que importa",
            BenefitsDescription = "Menos informação espalhada, mais contexto para decidir, dividir e concluir.",
            StepsTitle = "Como o HomePit entra na rotina",
            StepsDescription = "Uma estrutura simples para sair das ideias e chegar ao trabalho concluído.",
            HighlightEyebrow = "Contexto compartilhado",
            HighlightTitle = "Projetos, pessoas e conhecimento no mesmo lugar.",
            HighlightDescription = "Cada casa mantém seus próprios universos, permissões e histórico. A equipe doméstica sabe o que está acontecendo e qual é o próximo passo.",
            FinalCtaTitle = "Sua casa merece uma operação mais leve.",
            FinalCtaDescription = "Converse com a HomePit e descubra como centralizar os projetos e rotinas da sua casa.",
            FooterText = "HomePit organiza a operação da casa para que as pessoas possam cuidar melhor do tempo, dos espaços e umas das outras.",
            HeroImageAlt = "Visão organizada dos projetos e atividades de uma casa no HomePit",
            HighlightImageAlt = "Pessoas colaborando na organização de projetos residenciais"
        };

        page.Benefits =
        [
            new InstitutionalBenefit
            {
                Position = 0,
                Title = "Estrutura que faz sentido",
                Description = "Organize a casa em universos, projetos, atividades e pendências sem perder o contexto."
            },
            new InstitutionalBenefit
            {
                Position = 1,
                Title = "Colaboração com clareza",
                Description = "Compartilhe responsabilidades e mantenha permissões coerentes para cada pessoa."
            },
            new InstitutionalBenefit
            {
                Position = 2,
                Title = "Conhecimento reutilizável",
                Description = "Guarde prompts, referências e aprendizados para usar novamente quando precisar."
            }
        ];

        page.Steps =
        [
            new InstitutionalStep
            {
                Position = 0,
                Title = "Crie a casa",
                Description = "Defina o espaço compartilhado e convide as pessoas que participam da rotina."
            },
            new InstitutionalStep
            {
                Position = 1,
                Title = "Organize os projetos",
                Description = "Agrupe reformas, manutenção, compras e planos em estruturas fáceis de navegar."
            },
            new InstitutionalStep
            {
                Position = 2,
                Title = "Acompanhe o avanço",
                Description = "Distribua atividades, registre pendências e mantenha todos alinhados."
            }
        ];

        return page;
    }

    private static NormalizedInstitutionalPage ValidateAndNormalize(UpdateInstitutionalPageRequest request)
    {
        if (request.Benefits is null)
        {
            throw new ValidationException("Informe os benefícios da página institucional.");
        }

        if (request.Steps is null)
        {
            throw new ValidationException("Informe as etapas da página institucional.");
        }

        return new NormalizedInstitutionalPage(
            RequiredText(request.SeoTitle, 160, "Informe o título de SEO."),
            RequiredText(request.SeoDescription, 320, "Informe a descrição de SEO."),
            RequiredText(request.BrandName, 80, "Informe o nome da marca."),
            RequiredText(request.BrandTagline, 200, "Informe a assinatura da marca."),
            RequiredText(request.HeroEyebrow, 120, "Informe o destaque do hero."),
            RequiredText(request.HeroTitle, 240, "Informe o título do hero."),
            RequiredText(request.HeroDescription, 1200, "Informe a descrição do hero."),
            RequiredText(request.PrimaryCtaLabel, 80, "Informe o texto do botão principal."),
            NormalizeHttpUrl(request.PrimaryCtaUrl),
            RequiredText(request.BenefitsTitle, 200, "Informe o título dos benefícios."),
            RequiredText(request.BenefitsDescription, 600, "Informe a descrição dos benefícios."),
            NormalizeItems(request.Benefits, "benefícios"),
            RequiredText(request.StepsTitle, 200, "Informe o título das etapas."),
            RequiredText(request.StepsDescription, 600, "Informe a descrição das etapas."),
            NormalizeItems(request.Steps, "etapas"),
            RequiredText(request.HighlightEyebrow, 120, "Informe o destaque da seção do produto."),
            RequiredText(request.HighlightTitle, 240, "Informe o título da seção do produto."),
            RequiredText(request.HighlightDescription, 1200, "Informe a descrição da seção do produto."),
            RequiredText(request.FinalCtaTitle, 240, "Informe o título da chamada final."),
            RequiredText(request.FinalCtaDescription, 1200, "Informe a descrição da chamada final."),
            RequiredText(request.FooterText, 600, "Informe o texto do rodapé."),
            RequiredText(request.HeroImageAlt, 300, "Informe o texto alternativo da imagem principal."),
            RequiredText(request.HighlightImageAlt, 300, "Informe o texto alternativo da imagem de destaque."));
    }

    private static IReadOnlyCollection<NormalizedItem> NormalizeItems(
        IReadOnlyCollection<InstitutionalContentItemRequest> items,
        string label)
    {
        if (items.Count is < MinimumListItems or > MaximumListItems)
        {
            throw new ValidationException($"Informe entre {MinimumListItems} e {MaximumListItems} {label}.");
        }

        return items
            .Select((item, position) => new NormalizedItem(
                position,
                RequiredText(item.Title, 160, $"Informe o título de todos os {label}."),
                RequiredText(item.Description, 600, $"Informe a descrição de todos os {label}.")))
            .ToArray();
    }

    private void ApplyContent(InstitutionalPage page, NormalizedInstitutionalPage content)
    {
        page.SeoTitle = content.SeoTitle;
        page.SeoDescription = content.SeoDescription;
        page.BrandName = content.BrandName;
        page.BrandTagline = content.BrandTagline;
        page.HeroEyebrow = content.HeroEyebrow;
        page.HeroTitle = content.HeroTitle;
        page.HeroDescription = content.HeroDescription;
        page.PrimaryCtaLabel = content.PrimaryCtaLabel;
        page.PrimaryCtaUrl = content.PrimaryCtaUrl;
        page.BenefitsTitle = content.BenefitsTitle;
        page.BenefitsDescription = content.BenefitsDescription;
        page.StepsTitle = content.StepsTitle;
        page.StepsDescription = content.StepsDescription;
        page.HighlightEyebrow = content.HighlightEyebrow;
        page.HighlightTitle = content.HighlightTitle;
        page.HighlightDescription = content.HighlightDescription;
        page.FinalCtaTitle = content.FinalCtaTitle;
        page.FinalCtaDescription = content.FinalCtaDescription;
        page.FooterText = content.FooterText;
        page.HeroImageAlt = content.HeroImageAlt;
        page.HighlightImageAlt = content.HighlightImageAlt;

        SynchronizeBenefits(page, content.Benefits);
        SynchronizeSteps(page, content.Steps);
    }

    private void SynchronizeBenefits(
        InstitutionalPage page,
        IReadOnlyCollection<NormalizedItem> items)
    {
        var existing = page.Benefits.OrderBy(item => item.Position).ToArray();
        var incoming = items.OrderBy(item => item.Position).ToArray();

        for (var index = 0; index < incoming.Length; index++)
        {
            if (index < existing.Length)
            {
                existing[index].Position = incoming[index].Position;
                existing[index].Title = incoming[index].Title;
                existing[index].Description = incoming[index].Description;
                continue;
            }

            page.Benefits.Add(new InstitutionalBenefit
            {
                Position = incoming[index].Position,
                Title = incoming[index].Title,
                Description = incoming[index].Description
            });
        }

        foreach (var item in existing.Skip(incoming.Length))
        {
            db.InstitutionalBenefits.Remove(item);
            page.Benefits.Remove(item);
        }
    }

    private void SynchronizeSteps(
        InstitutionalPage page,
        IReadOnlyCollection<NormalizedItem> items)
    {
        var existing = page.Steps.OrderBy(item => item.Position).ToArray();
        var incoming = items.OrderBy(item => item.Position).ToArray();

        for (var index = 0; index < incoming.Length; index++)
        {
            if (index < existing.Length)
            {
                existing[index].Position = incoming[index].Position;
                existing[index].Title = incoming[index].Title;
                existing[index].Description = incoming[index].Description;
                continue;
            }

            page.Steps.Add(new InstitutionalStep
            {
                Position = incoming[index].Position,
                Title = incoming[index].Title,
                Description = incoming[index].Description
            });
        }

        foreach (var item in existing.Skip(incoming.Length))
        {
            db.InstitutionalSteps.Remove(item);
            page.Steps.Remove(item);
        }
    }

    private static string RequiredText(string? value, int maxLength, string message)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is null)
        {
            throw new ValidationException(message);
        }

        if (normalized.Length > maxLength)
        {
            throw new ValidationException($"O texto deve ter no máximo {maxLength} caracteres.");
        }

        return normalized;
    }

    private static string NormalizeHttpUrl(string? value)
    {
        var normalized = RequiredText(value, 2000, "Informe o link do botão principal.");
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ValidationException("Informe um link HTTP ou HTTPS válido para o botão principal.");
        }

        return uri.ToString();
    }

    private static string NormalizeImageContentType(string slot, string? contentType)
    {
        var normalized = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim();
        if (normalized is null)
        {
            throw new ValidationException(slot == SeoSlot
                ? "A imagem de SEO deve estar em WEBP."
                : "A imagem institucional deve estar em JPG, PNG ou WEBP.");
        }

        if (slot == SeoSlot && !AllowedSeoImageContentTypes.Contains(normalized))
        {
            throw new ValidationException("A imagem de SEO deve estar em WEBP.");
        }

        if (slot != SeoSlot && !AllowedImageContentTypes.Contains(normalized))
        {
            throw new ValidationException("A imagem institucional deve estar em JPG, PNG ou WEBP.");
        }

        return normalized.ToLowerInvariant();
    }

    private static string NormalizeSlot(string slot)
    {
        return slot.Trim().ToLowerInvariant() switch
        {
            "hero" => "hero",
            "highlight" => "highlight",
            SeoSlot => SeoSlot,
            _ => throw new ValidationException("Use o slot de imagem 'hero', 'highlight' ou 'seo'.")
        };
    }

    private static async Task<byte[]> ReadSeoImageBufferAsync(Stream content, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static void EnsureSeoImageDimensions(byte[] buffer)
    {
        var size = TryReadWebpDimensions(buffer);
        if (size is null || size.Value.Width != SeoImageWidth || size.Value.Height != SeoImageHeight)
        {
            throw new ValidationException("A imagem de SEO deve estar em WEBP com resolução exata de 1200 x 630 px.");
        }
    }

    private static (int Width, int Height)? TryReadWebpDimensions(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 30 ||
            !buffer[..4].SequenceEqual(RiffHeader) ||
            !buffer.Slice(8, 4).SequenceEqual(WebpHeader))
        {
            return null;
        }

        var chunkType = buffer.Slice(12, 4);
        if (chunkType.SequenceEqual(Vp8XHeader))
        {
            return ReadVp8XDimensions(buffer);
        }

        if (chunkType.SequenceEqual(Vp8LHeader))
        {
            return ReadVp8LDimensions(buffer);
        }

        if (chunkType.SequenceEqual(Vp8Header))
        {
            return ReadVp8Dimensions(buffer);
        }

        return null;
    }

    private static (int Width, int Height)? ReadVp8XDimensions(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 30)
        {
            return null;
        }

        var width = 1 + buffer[24] + (buffer[25] << 8) + (buffer[26] << 16);
        var height = 1 + buffer[27] + (buffer[28] << 8) + (buffer[29] << 16);
        return (width, height);
    }

    private static (int Width, int Height)? ReadVp8LDimensions(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 25 || buffer[20] != 0x2F)
        {
            return null;
        }

        var b1 = buffer[21];
        var b2 = buffer[22];
        var b3 = buffer[23];
        var b4 = buffer[24];
        var width = 1 + b1 + ((b2 & 0x3F) << 8);
        var height = 1 + ((b2 >> 6) | (b3 << 2) | ((b4 & 0x0F) << 10));
        return (width, height);
    }

    private static (int Width, int Height)? ReadVp8Dimensions(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 30 ||
            buffer[23] != 0x9D ||
            buffer[24] != 0x01 ||
            buffer[25] != 0x2A)
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(26, 2)) & 0x3FFF;
        var height = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(28, 2)) & 0x3FFF;
        return (width, height);
    }

    private static void ApplyImageMetadata(
        InstitutionalPage page,
        string slot,
        string objectKey,
        string contentType,
        DateTimeOffset updatedAt)
    {
        switch (slot)
        {
            case "hero":
                page.HeroImageObjectKey = objectKey;
                page.HeroImageContentType = contentType;
                page.HeroImageUpdatedAt = updatedAt;
                break;
            case "highlight":
                page.HighlightImageObjectKey = objectKey;
                page.HighlightImageContentType = contentType;
                page.HighlightImageUpdatedAt = updatedAt;
                break;
            case SeoSlot:
                page.SeoImageObjectKey = objectKey;
                page.SeoImageContentType = contentType;
                page.SeoImageUpdatedAt = updatedAt;
                break;
        }
    }

    private static void ClearImageMetadata(InstitutionalPage page, string slot)
    {
        switch (slot)
        {
            case "hero":
                page.HeroImageObjectKey = null;
                page.HeroImageContentType = null;
                page.HeroImageUpdatedAt = null;
                break;
            case "highlight":
                page.HighlightImageObjectKey = null;
                page.HighlightImageContentType = null;
                page.HighlightImageUpdatedAt = null;
                break;
            case SeoSlot:
                page.SeoImageObjectKey = null;
                page.SeoImageContentType = null;
                page.SeoImageUpdatedAt = null;
                break;
        }
    }

    private void EnsureSuperAdmin()
    {
        if (userContext.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Somente o superadmin pode gerenciar a página institucional.");
        }
    }

    private sealed record NormalizedItem(int Position, string Title, string Description);

    private sealed record NormalizedInstitutionalPage(
        string SeoTitle,
        string SeoDescription,
        string BrandName,
        string BrandTagline,
        string HeroEyebrow,
        string HeroTitle,
        string HeroDescription,
        string PrimaryCtaLabel,
        string PrimaryCtaUrl,
        string BenefitsTitle,
        string BenefitsDescription,
        IReadOnlyCollection<NormalizedItem> Benefits,
        string StepsTitle,
        string StepsDescription,
        IReadOnlyCollection<NormalizedItem> Steps,
        string HighlightEyebrow,
        string HighlightTitle,
        string HighlightDescription,
        string FinalCtaTitle,
        string FinalCtaDescription,
        string FooterText,
        string HeroImageAlt,
        string HighlightImageAlt);
}
