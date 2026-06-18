using FluentValidation;
using Facturacion.Api.Models;

namespace Facturacion.Api.Validators
{
    public class EmitirFacturaValidator : AbstractValidator<EmitirFacturaRequest>
    {
        public EmitirFacturaValidator()
        {
            RuleFor(x => x.EmpresaRuc).NotEmpty().Length(13).WithMessage("El RUC del emisor debe tener 13 dígitos.");
            RuleFor(x => x.Estab).NotEmpty().Length(3);
            RuleFor(x => x.PtoEmi).NotEmpty().Length(3);
            RuleFor(x => x.Ambiente).IsInEnum();
            RuleFor(x => x.FechaEmision).NotEmpty();
            RuleFor(x => x.RazonSocialComprador).NotEmpty();
            RuleFor(x => x.IdentificacionComprador).NotEmpty().MaximumLength(20);
            RuleFor(x => x.TipoIdentificacion).IsInEnum();

            RuleFor(x => x.Detalles).NotEmpty().WithMessage("La factura debe tener al menos un detalle.");
            RuleForEach(x => x.Detalles).SetValidator(new DetalleValidator());

            RuleFor(x => x.FormasPago).NotEmpty().WithMessage("La factura debe tener al menos una forma de pago.");
        }
    }

    public class DetalleValidator : AbstractValidator<DetalleRequest>
    {
        public DetalleValidator()
        {
            RuleFor(x => x.CodigoPrincipal).NotEmpty().MaximumLength(25);
            RuleFor(x => x.Descripcion).NotEmpty();
            RuleFor(x => x.Cantidad).GreaterThan(0m);
            RuleFor(x => x.PrecioUnitario).GreaterThanOrEqualTo(0m);
            RuleFor(x => x.Descuento).GreaterThanOrEqualTo(0m);
            RuleFor(x => x.CodigoIva).IsInEnum();
        }
    }
}
