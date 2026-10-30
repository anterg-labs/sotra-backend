using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api.DTOs.Activos;
using Api.DTOs.Clientes;
using Api.DTOs.DocumentosTecnicos;
using Api.DTOs.ModelosMaquina;
using Api.DTOs.OrdenesTrabajo;
using Api.DTOs.Repuestos;
using Api.DTOs.TiposMaquina;
using Api.DTOs.Usuarios;

namespace Api.PruebasUnitarias;

public class ContratosDeActualizacionPruebas
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static IEnumerable<TestCaseData> Formularios()
    {
        yield return new(typeof(ClienteActualizarDto), """{"razonSocial":"Empresa","nombreContacto":"Ana","correo":null,"telefono":null,"direccion":null,"observaciones":null}""");
        yield return new(typeof(ActivoActualizarDto), """{"clienteId":1,"numSerie":"SN-1","descripcion":null,"modeloMaquinaId":1,"anioFabricacion":null,"nombre":null,"ubicacion":null,"estado":"activo"}""");
        yield return new(typeof(ModeloMaquinaActualizarDto), """{"nombre":"Modelo","tipoMaquinaId":1,"descripcion":null,"estado":"activo"}""");
        yield return new(typeof(TipoMaquinaActualizarDto), """{"nombre":"Tipo","estado":"activo"}""");
        yield return new(typeof(UsuarioActualizarDto), """{"nombre":"Ana","apellido":"Pérez","correo":"ana@example.com","telefono":"99123456","idsRol":[1],"estado":"activo","fechaIngreso":"2026-10-21"}""");
        yield return new(typeof(RepuestoActualizarDto), """{"codigo":"REP-1","nombre":"Filtro","descripcion":null,"marca":null,"ubicacion":null,"categoria":null,"modeloCompatibilidad":null,"unidadMedida":null,"observaciones":null,"cantidadMinima":0}""");
        yield return new(typeof(DocumentoTecnicoActualizarDto), """{"titulo":null,"tipo":"manual_tecnico","descripcion":null,"activoId":null,"modeloMaquinaId":null}""");
        yield return new(typeof(OrdenTrabajoActualizarDto), """{"clienteId":1,"activoId":1,"tecnicoId":null,"tipo":"correctivo","prioridad":"media","descripcion":"Revisar motor","observaciones":null,"fechaPrevistaEjecucion":"2026-10-21","sugerencias":[],"repuestos":[],"estado":"pendiente"}""");
    }

    [TestCaseSource(nameof(Formularios))]
    public void FormularioCompletoAceptaOpcionalesExplicitamenteNulos(Type tipo, string cuerpo)
    {
        var solicitud = JsonSerializer.Deserialize(cuerpo, tipo, Json)!;
        List<ValidationResult> errores = [];
        Assert.That(Validator.TryValidateObject(solicitud, new(solicitud), errores, true), Is.True,
            string.Join("; ", errores.Select(e => e.ErrorMessage)));
    }

    [TestCaseSource(nameof(Formularios))]
    public void PutIncompletoRechazaCadaCampoOmitidoInclusoSiEsOpcional(Type tipo, string cuerpo)
    {
        var completo = JsonNode.Parse(cuerpo)!.AsObject();
        foreach (var campo in completo.Select(p => p.Key).ToArray())
        {
            var incompleto = completo.DeepClone().AsObject();
            incompleto.Remove(campo);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(incompleto.ToJsonString(), tipo, Json), campo);
        }
    }

    [TestCaseSource(nameof(Formularios))]
    public void CamposObligatoriosRechazanNullEnLugarDeAplicarValoresPorDefecto(Type tipo, string cuerpo)
    {
        var completo = JsonNode.Parse(cuerpo)!.AsObject();
        foreach (var campo in completo.Where(p => p.Value is not null).Select(p => p.Key).ToArray())
        {
            var invalido = completo.DeepClone().AsObject();
            invalido[campo] = null;
            try
            {
                var solicitud = JsonSerializer.Deserialize(invalido.ToJsonString(), tipo, Json)!;
                List<ValidationResult> errores = [];
                Assert.That(Validator.TryValidateObject(solicitud, new(solicitud), errores, true), Is.False, campo);
            }
            catch (JsonException)
            {
                // Los tipos numéricos y las fechas no anulables se rechazan al deserializar.
            }
        }
    }

    [TestCase(typeof(StockMinimoDto))]
    [TestCase(typeof(DocumentoTecnicoVigenciaDto))]
    [TestCase(typeof(AsignacionTecnicoDto))]
    public void PatchSinValorNoAplicaCeroNiFalsePorDefecto(Type tipo)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{}", tipo, Json));
    }
}
