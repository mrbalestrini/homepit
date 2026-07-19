using OrganizaClub.Domain.Common;

namespace OrganizaClub.Domain.Institutional;

public sealed class InstitutionalPage : AuditableEntity
{
    public string Slug { get; set; } = "home";
    public string SeoTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public string? SeoImageObjectKey { get; set; }
    public string? SeoImageContentType { get; set; }
    public DateTimeOffset? SeoImageUpdatedAt { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string BrandTagline { get; set; } = string.Empty;
    public string HeroEyebrow { get; set; } = string.Empty;
    public string HeroTitle { get; set; } = string.Empty;
    public string HeroDescription { get; set; } = string.Empty;
    public string PrimaryCtaLabel { get; set; } = string.Empty;
    public string PrimaryCtaUrl { get; set; } = string.Empty;
    public string BenefitsTitle { get; set; } = string.Empty;
    public string BenefitsDescription { get; set; } = string.Empty;
    public string StepsTitle { get; set; } = string.Empty;
    public string StepsDescription { get; set; } = string.Empty;
    public string HighlightEyebrow { get; set; } = string.Empty;
    public string HighlightTitle { get; set; } = string.Empty;
    public string HighlightDescription { get; set; } = string.Empty;
    public string FinalCtaTitle { get; set; } = string.Empty;
    public string FinalCtaDescription { get; set; } = string.Empty;
    public string FooterText { get; set; } = string.Empty;
    public string HeroImageAlt { get; set; } = string.Empty;
    public string? HeroImageObjectKey { get; set; }
    public string? HeroImageContentType { get; set; }
    public DateTimeOffset? HeroImageUpdatedAt { get; set; }
    public string HighlightImageAlt { get; set; } = string.Empty;
    public string? HighlightImageObjectKey { get; set; }
    public string? HighlightImageContentType { get; set; }
    public DateTimeOffset? HighlightImageUpdatedAt { get; set; }
    public ICollection<InstitutionalBenefit> Benefits { get; set; } = [];
    public ICollection<InstitutionalStep> Steps { get; set; } = [];
}
