using FluentValidation;
using Facturacion.Api.Models;

namespace Facturacion.Api.Validators
{
    public class CrearParametrosValidator : AbstractValidator<CrearParametrosRequest>
    {
        public CrearParametrosValidator()
        {
            RuleFor(x => x.EmpresaRuc).NotEmpty().Length(13).WithMessage("El RUC de la empresa debe tener 13 dígitos.");
            RuleFor(x => x.Estab).NotEmpty().Length(3);
            RuleFor(x => x.PtoEmi).NotEmpty().Length(3);
            // DireccionEstablecimiento y ContribuyenteEspecial son opcionales (nullable en BD).
        }
    }
}
