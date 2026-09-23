using Api.DTOs.Roles;
using Api.Mapeadores;
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

    [HttpGet]
    [ProducesResponseType<List<RolDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RolDto>>> ObtenerTodos(
        CancellationToken cancellationToken)
    {
        var respuesta = await _rolDao.ListarTodoAsync(
            RolMapeador.Proyeccion, cancellationToken);
        return Ok(respuesta);
    }
}
