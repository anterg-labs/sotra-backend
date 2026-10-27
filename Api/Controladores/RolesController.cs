using Api.DTOs.Roles;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/roles")]
public class RolesController : ControllerBase
{
    private readonly RolDAO _rolDao;

    public RolesController(RolDAO rolDao)
    {
        _rolDao = rolDao;
    }

    /// <summary>Lista los roles de SOTRA.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.UsuarioConsultar)]
    [ProducesResponseType<List<RolDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RolDto>>> ObtenerTodos(CancellationToken cancelacion)
    {
        var respuesta = new List<RolDto>();
        foreach (var rol in await _rolDao.ListarTodoAsync(cancelacion))
        {
            respuesta.Add(RolMapeador.ADto(rol));
        }

        return Ok(respuesta);
    }
}
