namespace HomePit.Application.Institutional;

public sealed record InstitutionalContentItemDto(
    int Position,
    string Title,
    string Description);

public sealed record InstitutionalPageDto(
    string Slug,
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
    IReadOnlyCollection<InstitutionalContentItemDto> Benefits,
    string StepsTitle,
    string StepsDescription,
    IReadOnlyCollection<InstitutionalContentItemDto> Steps,
    string HighlightEyebrow,
    string HighlightTitle,
    string HighlightDescription,
    string FinalCtaTitle,
    string FinalCtaDescription,
    string FooterText,
    string HeroImageAlt,
    bool HasHeroImage,
    DateTimeOffset? HeroImageUpdatedAt,
    string HighlightImageAlt,
    bool HasHighlightImage,
    DateTimeOffset? HighlightImageUpdatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record InstitutionalContentItemRequest(
    string Title,
    string Description);

public sealed record UpdateInstitutionalPageRequest(
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
    IReadOnlyCollection<InstitutionalContentItemRequest> Benefits,
    string StepsTitle,
    string StepsDescription,
    IReadOnlyCollection<InstitutionalContentItemRequest> Steps,
    string HighlightEyebrow,
    string HighlightTitle,
    string HighlightDescription,
    string FinalCtaTitle,
    string FinalCtaDescription,
    string FooterText,
    string HeroImageAlt,
    string HighlightImageAlt);
