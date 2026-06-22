using FluentValidation;
using Facturacion.Api.Models;

namespace Facturacion.Api.Validators
{
    public class CrearEmpresaValidator : AbstractValidator<CrearEmpresaRequest>
    {
        public CrearEmpresaValidator()
        {
            RuleFor(x => x.Ruc).NotEmpty().Length(13).WithMessage("El RUC debe tener 13 dígitos.");
            RuleFor(x => x.RazonSocial).NotEmpty().MaximumLength(300);
            RuleFor(x => x.DirMatriz).NotEmpty().MaximumLength(300);
            RuleFor(x => x.CertificadoPath).NotEmpty().MaximumLength(500);
            RuleFor(x => x.CertPassword).NotEmpty();
            // NombreComercial es opcional (nullable en BD).
        }
    }
}
