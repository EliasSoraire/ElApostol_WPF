using Microsoft.Data.SqlClient;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.Data
{
    public class VentaRepository
    {
        public static decimal RecargoTarjeta { get; set; } = 0.33m;

        public List<Producto> BuscarParaVenta(string busqueda)
        {
            var lista = new List<Producto>();
            if (string.IsNullOrWhiteSpace(busqueda)) return lista;

            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT p.id, p.codigo_interno, p.nombre, p.marca,
                       p.categoria_id, c.nombre AS nombre_categoria, p.proveedor,
                       v.id AS var_id, v.medida, v.calidad,
                       v.precio_unitario, v.stock_actual, v.stock_minimo
                FROM productos p
                LEFT JOIN categorias c ON c.id = p.categoria_id
                LEFT JOIN variantes_producto v ON v.producto_id = p.id
                WHERE (p.nombre LIKE @q OR p.codigo_interno LIKE @q)
                  AND v.stock_actual > 0
                ORDER BY p.nombre, v.medida", conn);

            cmd.Parameters.AddWithValue("@q", $"%{busqueda}%");

            using var r = cmd.ExecuteReader();
            var dic = new Dictionary<int, Producto>();

            while (r.Read())
            {
                var pid = r.GetInt32(r.GetOrdinal("id"));
                if (!dic.TryGetValue(pid, out var prod))
                {
                    prod = new Producto
                    {
                        Id = pid,
                        CodigoInterno = r.GetString(r.GetOrdinal("codigo_interno")),
                        Nombre = r.GetString(r.GetOrdinal("nombre")),
                        Marca = r.IsDBNull(r.GetOrdinal("marca")) ? null : r.GetString(r.GetOrdinal("marca")),
                        CategoriaId = r.IsDBNull(r.GetOrdinal("categoria_id")) ? 0 : r.GetInt32(r.GetOrdinal("categoria_id")),
                        NombreCategoria = r.IsDBNull(r.GetOrdinal("nombre_categoria")) ? null : r.GetString(r.GetOrdinal("nombre_categoria")),
                        Proveedor = r.IsDBNull(r.GetOrdinal("proveedor")) ? null : r.GetString(r.GetOrdinal("proveedor")),
                    };
                    dic[pid] = prod;
                    lista.Add(prod);
                }

                if (!r.IsDBNull(r.GetOrdinal("var_id")))
                {
                    prod.Variantes.Add(new VarianteProducto
                    {
                        Id = r.GetInt32(r.GetOrdinal("var_id")),
                        ProductoId = pid,
                        Medida = r.IsDBNull(r.GetOrdinal("medida")) ? null : r.GetString(r.GetOrdinal("medida")),
                        Calidad = r.IsDBNull(r.GetOrdinal("calidad")) ? null : r.GetString(r.GetOrdinal("calidad")),
                        PrecioUnitario = r.GetDecimal(r.GetOrdinal("precio_unitario")),
                        StockActual = r.GetInt32(r.GetOrdinal("stock_actual")),
                        StockMinimo = r.GetInt32(r.GetOrdinal("stock_minimo")),
                    });
                }
            }

            return lista;
        }

        public string GenerarNumeroComprobante()
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(
                    TRY_CAST(SUBSTRING(numero_comprobante, 5, LEN(numero_comprobante)) AS INT)
                ), 0)
                FROM ventas
                WHERE numero_comprobante LIKE 'VTA-%'", conn);

            var ultimo = (int)cmd.ExecuteScalar();
            return $"VTA-{(ultimo + 1):D5}";
        }

        public int GuardarVenta(Venta venta)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmdV = new SqlCommand(@"
                    INSERT INTO ventas
                        (numero_comprobante, fecha_hora, usuario_id,
                         metodo_pago, subtotal, monto_recargo, total, observaciones)
                    VALUES
                        (@nro, @fecha, @uid,
                         @metodo, @sub, @recargo, @total, @obs);
                    SELECT SCOPE_IDENTITY();", conn, tx);

                cmdV.Parameters.AddWithValue("@nro", venta.NumeroComprobante);
                cmdV.Parameters.AddWithValue("@fecha", venta.FechaHora);
                cmdV.Parameters.AddWithValue("@uid", venta.UsuarioId);
                cmdV.Parameters.AddWithValue("@metodo", venta.MetodoPago);
                cmdV.Parameters.AddWithValue("@sub", venta.Subtotal);
                cmdV.Parameters.AddWithValue("@recargo", venta.MontoRecargo);
                cmdV.Parameters.AddWithValue("@total", venta.Total);
                cmdV.Parameters.AddWithValue("@obs", (object?)venta.Observaciones ?? DBNull.Value);

                var ventaId = Convert.ToInt32(cmdV.ExecuteScalar());

                foreach (var d in venta.Detalles)
                {
                    var cmdD = new SqlCommand(@"
                        INSERT INTO detalle_ventas
                            (venta_id, variante_id, cantidad,
                             precio_unitario_historico, subtotal_linea)
                        VALUES
                            (@vid, @varid, @cant, @precio, @sub)", conn, tx);

                    cmdD.Parameters.AddWithValue("@vid", ventaId);
                    cmdD.Parameters.AddWithValue("@varid", d.VarianteId);
                    cmdD.Parameters.AddWithValue("@cant", d.Cantidad);
                    cmdD.Parameters.AddWithValue("@precio", d.PrecioUnitarioHistorico);
                    cmdD.Parameters.AddWithValue("@sub", d.SubtotalLinea);
                    cmdD.ExecuteNonQuery();

                    var cmdS = new SqlCommand(@"
                        UPDATE variantes_producto
                        SET stock_actual = stock_actual - @cant
                        WHERE id = @varid", conn, tx);

                    cmdS.Parameters.AddWithValue("@cant", d.Cantidad);
                    cmdS.Parameters.AddWithValue("@varid", d.VarianteId);
                    cmdS.ExecuteNonQuery();
                }

                tx.Commit();
                return ventaId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public int ObtenerVentasHoyEmpleado(int usuarioId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            var cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM ventas
                WHERE usuario_id = @id
                  AND CAST(fecha_hora AS DATE) = CAST(GETDATE() AS DATE)", conn);
            cmd.Parameters.AddWithValue("@id", usuarioId);
            return (int)cmd.ExecuteScalar();
        }

        public decimal ObtenerTotalHoyEmpleado(int usuarioId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            var cmd = new SqlCommand(@"
                SELECT ISNULL(SUM(total), 0) FROM ventas
                WHERE usuario_id = @id
                  AND CAST(fecha_hora AS DATE) = CAST(GETDATE() AS DATE)", conn);
            cmd.Parameters.AddWithValue("@id", usuarioId);
            return (decimal)cmd.ExecuteScalar();
        }

        public ResumenTurno ObtenerResumenTurno(int usuarioId, DateTime desde)
        {
            var resumen = new ResumenTurno();

            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            using (var cmd = new SqlCommand(@"
                SELECT COUNT(*), ISNULL(SUM(total), 0)
                FROM ventas
                WHERE usuario_id = @uid AND fecha_hora >= @desde", conn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cmd.Parameters.AddWithValue("@desde", desde);
                using var rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    resumen.CantidadVentas = rd.GetInt32(0);
                    resumen.TotalRecaudado = rd.GetDecimal(1);
                }
            }

            using (var cmd = new SqlCommand(@"
                SELECT TOP 1
                    v.id, v.numero_comprobante, v.fecha_hora,
                    v.metodo_pago, v.subtotal, v.monto_recargo, v.total,
                    (
                        SELECT TOP 1
                            p.nombre + ' · $' + CAST(CAST(dv.precio_unitario_historico AS int) AS varchar)
                        FROM detalle_ventas dv
                        JOIN variantes_producto vp ON vp.id = dv.variante_id
                        JOIN productos          p  ON p.id  = vp.producto_id
                        WHERE dv.venta_id = v.id
                    ) AS descripcion
                FROM ventas v
                WHERE v.usuario_id = @uid AND v.fecha_hora >= @desde
                ORDER BY v.fecha_hora DESC", conn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cmd.Parameters.AddWithValue("@desde", desde);
                using var rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    resumen.UltimaVenta = new Venta
                    {
                        Id = rd.GetInt32(0),
                        NumeroComprobante = rd.GetString(1),
                        FechaHora = rd.GetDateTime(2),
                        MetodoPago = rd.GetString(3),
                        Subtotal = rd.GetDecimal(4),
                        MontoRecargo = rd.GetDecimal(5),
                        Total = rd.GetDecimal(6),
                    };
                    resumen.UltimaVentaDesc = rd.IsDBNull(7) ? null : rd.GetString(7);
                }
            }

            return resumen;
        }

        public List<ElApostol_WPF.UI.Empleado.VentaCajaVM> ObtenerVentasDetalladas(
    int usuarioId, DateTime desde, DateTime hasta)
        {
            var lista = new List<ElApostol_WPF.UI.Empleado.VentaCajaVM>();

            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
        SELECT
            v.id,
            v.numero_comprobante,
            v.fecha_hora,
            v.metodo_pago,
            v.total,
            COUNT(dv.id) AS cantidad_items,
            (
                SELECT TOP 1 p.nombre
                FROM detalle_ventas dv2
                JOIN variantes_producto vp2 ON vp2.id = dv2.variante_id
                JOIN productos          p   ON p.id   = vp2.producto_id
                WHERE dv2.venta_id = v.id
                ORDER BY dv2.id
            ) AS primer_material,
            (
                SELECT TOP 1 vp2.medida
                FROM detalle_ventas dv2
                JOIN variantes_producto vp2 ON vp2.id = dv2.variante_id
                WHERE dv2.venta_id = v.id
                ORDER BY dv2.id
            ) AS primer_medida
        FROM ventas v
        LEFT JOIN detalle_ventas dv ON dv.venta_id = v.id
        WHERE v.usuario_id = @uid
          AND v.fecha_hora >= @desde
          AND v.fecha_hora <= @hasta
        GROUP BY v.id, v.numero_comprobante, v.fecha_hora,
                 v.metodo_pago, v.total
        ORDER BY v.fecha_hora DESC", conn);

            cmd.Parameters.AddWithValue("@uid", usuarioId);
            cmd.Parameters.AddWithValue("@desde", desde);
            cmd.Parameters.AddWithValue("@hasta", hasta);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new ElApostol_WPF.UI.Empleado.VentaCajaVM
                {
                    VentaId = r.GetInt32(0),
                    NumeroComprobante = r.GetString(1),
                    FechaHora = r.GetDateTime(2),
                    MetodoPago = r.GetString(3),
                    Total = r.GetDecimal(4),
                    CantidadItems = r.GetInt32(5),
                    PrimerMaterial = r.IsDBNull(6) ? "—" : r.GetString(6),
                    MedidaDisplay = r.IsDBNull(7) ? "—" : r.GetString(7),
                });
            }

            return lista;
        }

    }
}