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
    public ActionResult<List<RolDto>> ObtenerTodos()
    {
        var respuesta = new List<RolDto>();
        foreach (var rol in _rolDao.ListarTodo())
        {
            respuesta.Add(RolMapeador.ADto(rol));
        }

        return Ok(respuesta);
    }
}
