using System;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using Facturacion.Api.Controllers;

namespace Facturacion.Api.Composition
{
    // IHttpControllerActivator manual: construye los controllers de la API con sus
    // dependencias desde el CompositionRoot. Los controllers desconocidos (HelpPage,
    // scaffold) caen al activador por defecto.
    public class ResolvedorControladores : IHttpControllerActivator
    {
        private readonly DefaultHttpControllerActivator _porDefecto = new DefaultHttpControllerActivator();

        public IHttpController Create(HttpRequestMessage request, HttpControllerDescriptor descriptor, Type controllerType)
        {
            if (controllerType == typeof(FacturasController))
                return new FacturasController(
                    CompositionRoot.EmitirFactura(),
                    CompositionRoot.ReintentarEmisionFactura(),
                    CompositionRoot.Facturas(),
                    CompositionRoot.Auditoria());

            if (controllerType == typeof(EmpresasController))
                return new EmpresasController(CompositionRoot.Empresas(), CompositionRoot.Auditoria());

            if (controllerType == typeof(ParametrosController))
                return new ParametrosController(CompositionRoot.Parametros(), CompositionRoot.Secuenciales(), CompositionRoot.Auditoria());

            if (controllerType == typeof(HealthController))
                return new HealthController(CompositionRoot.Conexion());

            return _porDefecto.Create(request, descriptor, controllerType);
        }
    }
}
