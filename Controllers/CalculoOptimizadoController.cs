namespace WSOptimizer7.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualBasic;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using WSOptimizer7.App_Data;
    using WSOptimizer7.Config;
    using WSOptimizer7.Models;

    //[Route("[controller]")]

    public class CalculoOptimizadoController : Controller
    {
        public int ID_NUPIG_SEW = 1;
        public int ID_NUPIG_UNO = 2;
        public int ID_NUPIG_DOS = 3;
        public int ID_NUPIG_TRES = 4;
        public int ID_INICIADOR = 5;
        public int ID_CRECIMIENTO = 6;
        public int ID_DESARROLLO = 7;
        public int ID_ENGORDA = 8;
        public int ID_FINALIZADOR = 9;


        public RequestOptimizerModel objReq;
        public DataTable dtPdmax;
        public DataTable dtMD;
        public DataTable dtParam;
        public DataTable dtRef;
        public DataTable dtConst;


        [HttpPost]
        [Route("api/optimizado")]
        public RespOptimizerModel GetOptimizerModelN([FromBody] RequestOptimizerModel objReqDataIni)
        {

            string strSQLRef = "SELECT * FROM CatOptimizerC_Referencias ";
            dtRef = Database.execQuery(strSQLRef);

            string strSQLConst = "SELECT * FROM CatOptimizerC_Constantes ";
            dtConst = Database.execQuery(strSQLConst);

            string strSQLParam = "SELECT * FROM CatOptimizerC_ParametrosEconomicos ";
            dtParam = Database.execQuery(strSQLParam);
            objReqDataIni.IsOptimizar = false;
            objReqDataIni.EdadInicialTmp = objReqDataIni.EdadDestete;

            ProductoModel pModTmpV = objReqDataIni.Productos.Find(p => p.Costo == 0 && p.IsEtapa.Equals("S") && (p.DuracionMin > 0 || p.DuracionMax > 0));
            if (pModTmpV != null)
            {
                throw new Exception("Existen etapas que no traen costo, coloque la duración en 0");
            }


            List<ResponseOptModel> lstResult = new List<ResponseOptModel>();
            double minVal = Math.Round((((objReqDataIni.EdadVenta - objReqDataIni.EdadSalida) / (double)3) - 14) / (double)14) * 14;
            double maxVal = Math.Round((((objReqDataIni.EdadVenta - objReqDataIni.EdadSalida) / (double)3) + 21) / (double)7) * 7;
            DateTime date1 = DateTime.Now;

            objReq = objReqDataIni;
            int cveParametroSeleccionado = objReqDataIni.CveParametro;
            // obtengo las etapas inciiales
            List<TablaModel> lstProdIni;
            lstProdIni = GetEtapasIniciales(objReqDataIni);
            if (lstProdIni.Max(r => r.Edad_Final) > objReqDataIni.EdadSalida)
            {
                throw new Exception("La Edad de salida deseada es menor a la edad de salida real, verifique datos de entrada y etapas anteriores a Iniciador");
            }

            TablaModel prod5 = GetTablaOptimizerxProd5(objReqDataIni, 5, lstProdIni);
            lstProdIni.Add(prod5);
            double pesoFinal5 = 0;
            pesoFinal5 = prod5.Peso_Final;
            objReqDataIni.Productos.Find(r => r.Posicion == 5).PesoFinal = pesoFinal5;

            double EdadIni = objReqDataIni.EdadVenta;
            double EdadFin = objReqDataIni.EdadVenta;

            double edadResultante = EdadIni - prod5.Edad_Final;
            double edadTope = EdadIni;
            ParallelOptions myOptions = new ParallelOptions();
            myOptions.MaxDegreeOfParallelism = Environment.ProcessorCount;
            VarMinMaxModel valMinMax5 = new VarMinMaxModel(prod5.Peso_Final, 0, prod5.Edad_Final, 0);

            VarMinMaxModel valMinMax6 = GetMinMaxValues(objReqDataIni, valMinMax5, 6);
            VarMinMaxModel valMinMax7 = GetMinMaxValues(objReqDataIni, valMinMax6, 7);
            VarMinMaxModel valMinMax8 = GetMinMaxValues(objReqDataIni, valMinMax7, 8);
            VarMinMaxModel valMinMax9 = GetMinMaxValues(objReqDataIni, valMinMax8, 9);
            VarMinMaxModel valMinMax10 = GetMinMaxValues(objReqDataIni, valMinMax9, 10);

            ProductoModel prod6 = objReqDataIni.Productos.Find(p => p.CveProducto == 6);
            ProductoModel prod7 = objReqDataIni.Productos.Find(p => p.CveProducto == 7);
            ProductoModel prod8 = objReqDataIni.Productos.Find(p => p.CveProducto == 8);
            ProductoModel prod9 = objReqDataIni.Productos.Find(p => p.CveProducto == 9);
            ProductoModel prod10 = objReqDataIni.Productos.Find(p => p.CveProducto == 10);
            double intervalStep = Double.Parse( AppSetConfig.AppSetting["MaxIteracciones"]); ;
            double step6 = 1;
            double pDif6 = Math.Round(valMinMax6.ValorMaximo - valMinMax6.ValorMinimo) / intervalStep;
            if (pDif6 > 0) step6 = pDif6;

            double step7 = 1;
            double pDif7 = Math.Round(valMinMax7.ValorMaximo - valMinMax7.ValorMinimo) / intervalStep;
            if (pDif7 > 0) step7 = pDif7;

            double step8 = 1;
            double pDif8 = Math.Round(valMinMax8.ValorMaximo - valMinMax8.ValorMinimo) / intervalStep;
            if (pDif8 > 0) step8 = pDif8;

            double step9 = 1;
            double pDif9 = Math.Round(valMinMax9.ValorMaximo - valMinMax9.ValorMinimo) / intervalStep;
            if (pDif9 > 0) step9 =pDif9;


            int diasTmpRactopamina = 0;
            int diasTopeRactopamina = 0;
            if (prod10.DuracionMin > 0 || prod10.DuracionMax > 0)
            {
                if (prod10.DuracionMin > prod10.DuracionMax)
                {
                    throw new Exception("La duración de ractopamina es incorrecta");
                }
                else
                {
                    diasTmpRactopamina = (int)prod10.DuracionMin;
                    diasTopeRactopamina = (int)prod10.DuracionMax;
                }
            }

            for (var k = diasTmpRactopamina; k <= diasTopeRactopamina; k += 1)
            {
                for (var x = valMinMax6.ValorMinimo; x <= valMinMax6.ValorMaximo; x += step6)
                {

                    objReqDataIni.IsOptimizar = true;
                    objReqDataIni.PesoInicialTmp = prod5.Peso_Final;
                    objReqDataIni.EdadInicialTmp = prod5.Edad_Final;
                    prod6.PesoFinal = x;
                    TablaModel tModel6 = GetDataRegistro(prod6, objReqDataIni);
                    while (Math.Round(tModel6.Duracion_Etapa) < tModel6.Duracion_Minima)
                    {
                        x += 1;
                        prod6.PesoFinal = x;
                        tModel6 = GetDataRegistro(prod6, objReqDataIni);
                        if (Math.Round(tModel6.Duracion_Etapa) > tModel6.Duracion_Minima)
                        {
                            x -= 1;
                            prod6.PesoFinal = x;
                            tModel6 = GetDataRegistro(prod6, objReqDataIni);
                            break;
                        }
                    }
                    for (var y = valMinMax7.ValorMinimo; y <= valMinMax7.ValorMaximo; y += step7)
                    {
                        double edadTope7 = objReqDataIni.EdadVenta - objReqDataIni.Productos.FindAll(t => t.CveProducto > 7).Sum(t => t.DuracionMin);
                        if (y < x)
                            y = x;

                        objReqDataIni.PesoInicialTmp = tModel6.Peso_Final;
                        objReqDataIni.EdadInicialTmp = tModel6.Edad_Final;
                        prod7.PesoFinal = y;
                        TablaModel tModel7 = GetDataRegistro(prod7, objReqDataIni);
                        while (Math.Round(tModel7.Duracion_Etapa )+1< tModel7.Duracion_Minima)
                        {
                            y += 1;
                            prod7.PesoFinal = y;
                            tModel7 = GetDataRegistro(prod7, objReqDataIni);
                        }
                        if (System.Convert.ToDouble(tModel7.Edad_Final) > edadTope7 + 1 | Math.Round(tModel7.Duracion_Etapa) > tModel7.Duracion_Maxima)
                            break;
                        bool isUltimaEtapa = false;
                        double edadTope8 = objReqDataIni.EdadVenta - objReqDataIni.Productos.FindAll(t => t.CveProducto > 8).Sum(t => t.DuracionMin);                    
                        double edadTope8Min = objReqDataIni.EdadVenta - objReqDataIni.Productos.FindAll(t => t.CveProducto > 8).Sum(t => t.DuracionMax);                      
                        for (var z = y; z <= valMinMax8.ValorMaximo; z += step8)
                        {
                          
                            objReqDataIni.PesoInicialTmp = tModel7.Peso_Final;
                            objReqDataIni.EdadInicialTmp = tModel7.Edad_Final;
                            TablaModel tModel8;
                          
                            if (edadTope8Min == edadTope8)
                            {
                                // Quiere decir que es la última etapa y debe de obtener valor si no ya no ejecuto lo demás
                                double durObjetivo = edadTope8 - objReqDataIni.EdadInicialTmp;
                                if (durObjetivo > prod8.DuracionMax + 1)
                                    break;
                                tModel8 = GetPeso_FinalEtapa9_10(objReqDataIni, tModel7, prod8, edadTope8);
                                if (tModel8 == null) break;
                                z = prod8.PesoFinal;
                                if (isUltimaEtapa)
                                    break;
                                isUltimaEtapa = true;
                            }
                            else
                            {
                                prod8.PesoFinal = z;
                                tModel8 = GetDataRegistro(prod8, objReqDataIni);
                                var duracionFaltante8 = edadTope8 - tModel8.Edad_Final;

                                if (Math.Round(tModel8.Duracion_Etapa) > tModel8.Duracion_Maxima)
                                    break;

                                if (tModel8.Edad_Final < edadTope8Min)
                                {
                                    double dif8 = edadTope8Min - tModel8.Edad_Final;
                                    if (Math.Abs(dif8) > 1)
                                    {
                                        z += dif8;
                                        prod8.PesoFinal = z;
                                        tModel8 = GetDataRegistro(prod8, objReqDataIni);
                                    }
                                }
                            }

                            double edadTopeObjetivo = objReqDataIni.EdadVenta - k;

                            if (Math.Round(tModel8.Duracion_Etapa) >= tModel8.Duracion_Minima & Math.Round(tModel8.Duracion_Etapa) <= tModel8.Duracion_Maxima)
                            {
                              
                                TablaModel tModel9 = GetPeso_FinalEtapa9_10(objReqDataIni, tModel8, prod9, edadTopeObjetivo);
                                if (tModel9 != null && prod10 != null)
                                {
                                    TablaModel tModel10 = GetPeso_FinalEtapa9_10(objReqDataIni, tModel9, prod10, objReqDataIni.EdadVenta);
                                    if (tModel10 != null)
                                    {
                                        string yatenemosalgo = "";
                                        objReqDataIni.Productos.Find(r => r.Posicion == 6).PesoFinal = x;
                                        objReqDataIni.Productos.Find(r => r.Posicion == 7).PesoFinal = y;
                                        objReqDataIni.Productos.Find(r => r.Posicion == 8).PesoFinal = tModel8.Peso_Final;
                                        objReqDataIni.Productos.Find(r => r.Posicion == 9).PesoFinal = tModel9.Peso_Final;
                                        objReqDataIni.Productos.Find(r => r.Posicion == 10).PesoFinal = tModel10.Peso_Final;
                                        objReqDataIni.IsOptimizar = false;

                                     
                                        ResponseOptModel objR = new ResponseOptModel();
                                        List<TablaModel> tablaOpt = new List<TablaModel>();
                                        if (!(IsFueraRango(tModel6) || IsFueraRango(tModel7) || IsFueraRango(tModel8) || IsFueraRango(tModel9) || IsFueraRango(tModel10)))
                                        {

                                            tablaOpt.AddRange(lstProdIni);
                                            tablaOpt.Add(tModel6);
                                            tablaOpt.Add(tModel7);
                                            tablaOpt.Add(tModel8);
                                            tablaOpt.Add(tModel9);
                                            tablaOpt.Add(tModel10);
                                            objR.Optimizer = GetOptimizer(objReqDataIni, tablaOpt);
                                            // objR.ValorObjetivo = objResp.Optimizer.Find(Function(p) p.Orden = 7).Valor
                                            if (y > 945.30)
                                            {
                                                String algo = "espera";
                                            }
                                            objR.LstProducto.Add(new CatalogoModel("5", pesoFinal5.ToString()));
                                            objR.LstProducto.Add(new CatalogoModel("6", x.ToString()));
                                            objR.LstProducto.Add(new CatalogoModel("7", y.ToString()));
                                            objR.LstProducto.Add(new CatalogoModel("8", tModel8.Peso_Final.ToString()));
                                            objR.LstProducto.Add(new CatalogoModel("9", tModel9.Peso_Final.ToString()));
                                            objR.LstProducto.Add(new CatalogoModel("10", tModel10.Peso_Final.ToString()));
                                            lstResult.Add(objR);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            DateTime date2 = DateTime.Now;
            var ss = date2 - date1;

            double paraminteres = 0;
            if (lstResult.Count == 0)
                throw new Exception("No se encontró modelo optimizado");

            ResponseOptModel objRespModelOpt1 = lstResult.Find(p => p.Optimizer.Find(t => t.Orden == 1).Valor == lstResult.Max(r => r.Optimizer.Find(t => t.Orden == 1).Valor));
            ResponseOptModel objRespModelOpt2 = lstResult.Find(p => p.Optimizer.Find(t => t.Orden == 2).Valor == lstResult.Min(r => r.Optimizer.Find(t => t.Orden == 2).Valor));
            ResponseOptModel objRespModelOpt3 = lstResult.Find(p => p.Optimizer.Find(t => t.Orden == 3).Valor == lstResult.Min(r => r.Optimizer.Find(t => t.Orden == 3).Valor));
            ResponseOptModel objRespModelOpt4 = lstResult.Find(p => p.Optimizer.Find(t => t.Orden == 4).Valor == lstResult.Min(r => r.Optimizer.Find(t => t.Orden == 4).Valor));
            ResponseOptModel objRespModelOpt5 = lstResult.Find(p => p.Optimizer.Find(t => t.Orden == 5).Valor == lstResult.Max(r => r.Optimizer.Find(t => t.Orden == 5).Valor));
            ResponseOptModel objRespModelOpt6 = lstResult.Find(p => p.Optimizer.Find(t => t.Orden == 6).Valor == lstResult.Max(r => r.Optimizer.Find(t => t.Orden == 6).Valor));


            RespOptimizerModel objRespModel = new RespOptimizerModel();
            objReqDataIni.IsOptimizar = false;
            objRespModel.ResponseParametro.Add(GetOptimizadoResponse(objRespModelOpt1, objReqDataIni, 1));
            objRespModel.ResponseParametro.Add(GetOptimizadoResponse(objRespModelOpt2, objReqDataIni, 2));
            objRespModel.ResponseParametro.Add(GetOptimizadoResponse(objRespModelOpt3, objReqDataIni, 3));
            objRespModel.ResponseParametro.Add(GetOptimizadoResponse(objRespModelOpt4, objReqDataIni, 4));
            objRespModel.ResponseParametro.Add(GetOptimizadoResponse(objRespModelOpt5, objReqDataIni, 5));
            objRespModel.ResponseParametro.Add(GetOptimizadoResponse(objRespModelOpt6, objReqDataIni, 6));

            objRespModel.ResponseParametro.ForEach(p => p.Resultado.Seleccionado = (short)cveParametroSeleccionado);

            if (objReqDataIni.CvePlan > 0)
                SaveData(objRespModel, objReqDataIni, cveParametroSeleccionado);


            Console.WriteLine("Datos fin");
            return objRespModel;


        }

        private bool IsFueraRango(TablaModel tModel)
        {
            int duracionEtapa = (int)Math.Round(tModel.Duracion_Etapa);
            int durMin = (int)Math.Round(tModel.Duracion_Minima);
            int durMax = (int)Math.Round(tModel.Duracion_Maxima);
            if (duracionEtapa < durMin || duracionEtapa > durMax)
            {
                return true;
            }
            return false;
        }

        private ResponseOptimizerModel GetOptimizadoResponse(ResponseOptModel objRespModelOpt, RequestOptimizerModel objReqDataIni, int cveParametro)
        {
            objRespModelOpt.LstProducto.ForEach(r =>
            {
                objReqDataIni.Productos.Find(t => t.CveProducto == Int32.Parse(r.Clave)).PesoFinal = double.Parse(r.Valor);
            });
            double pF6 = objReqDataIni.Productos.Find(p => p.CveProducto == 6).PesoFinal;
            double pF7 = objReqDataIni.Productos.Find(p => p.CveProducto == 7).PesoFinal;
            double pF8 = objReqDataIni.Productos.Find(p => p.CveProducto == 8).PesoFinal;
            double pF9 = objReqDataIni.Productos.Find(p => p.CveProducto == 9).PesoFinal;
            double pF10 = objReqDataIni.Productos.Find(p => p.CveProducto == 10).PesoFinal;
            ResponseOptimizerModel respOpt;
            objReqDataIni.CveParametro = cveParametro;
            respOpt = GetDataOptizerService(objReqDataIni);
            respOpt.CveParametro = cveParametro;
            respOpt.Parametro = GetParametroEcoVal(cveParametro, "NomParametro");


            return respOpt;
        }
        private TablaModel GetPeso_FinalEtapa9_10(RequestOptimizerModel objReqDataIni, TablaModel tModelAnt, ProductoModel prod, double edadTopeObjetivo)
        {
            TablaModel tModel9 = new TablaModel();
            objReqDataIni.PesoInicialTmp = tModelAnt.Peso_Final;
            objReqDataIni.EdadInicialTmp = tModelAnt.Edad_Final;
            double durObjetivo = edadTopeObjetivo - objReqDataIni.EdadInicialTmp;
            if (prod.DuracionMin == 0 & prod.DuracionMax == 0)
            {
                prod.PesoFinal = tModelAnt.Peso_Final;
                tModel9 = GetDataRegistro(prod, objReqDataIni);
                return tModel9;
            }
            if (Math.Round(durObjetivo) >= prod.DuracionMax + 1 | Math.Round(durObjetivo) < prod.DuracionMin)
            {
                return null;
            }
            //if (prod.DuracionMin == prod.DuracionMax) {
            //   durObjetivo = prod.DuracionMax; 
            //}

            prod.PesoFinal = objReqDataIni.PesoInicialTmp + durObjetivo; // más o menos corresponde
            tModel9 = GetDataRegistro(prod, objReqDataIni);
            double difObjetivo = tModel9.Duracion_Etapa - durObjetivo;
            if (tModel9.Duracion_Etapa < durObjetivo)
            {
                while ((tModel9.Duracion_Etapa < durObjetivo))
                {
                    if (prod.DuracionMin == prod.DuracionMax)
                    {
                        if ((int)Math.Round(tModel9.Duracion_Etapa) == durObjetivo)
                        {
                            return tModel9;
                        }

                    }
                    difObjetivo = durObjetivo - tModel9.Duracion_Etapa;
                    if (difObjetivo > 2)
                        prod.PesoFinal += difObjetivo - 1;
                    else
                        prod.PesoFinal += 1;

                    if (prod.PesoFinal > 140)
                        return tModel9;
                    tModel9 = GetDataRegistro(prod, objReqDataIni);
                    difObjetivo = durObjetivo - tModel9.Duracion_Etapa;
                }
                double difObjetivoFinal = tModel9.Duracion_Etapa - durObjetivo;
                if (difObjetivoFinal > 0.5)
                {
                    while ((tModel9.Duracion_Etapa > durObjetivo))
                    {
                        if (prod.DuracionMin == prod.DuracionMax)
                        {
                            if ((int)Math.Round(tModel9.Duracion_Etapa) == durObjetivo)
                            {
                                return tModel9;
                            }

                        }
                        prod.PesoFinal -= 0.5;
                        tModel9 = GetDataRegistro(prod, objReqDataIni);
                        if (tModel9.Duracion_Minima == tModel9.Duracion_Maxima && (int)Math.Round(tModel9.Duracion_Etapa) == tModel9.Duracion_Minima)
                        {
                            return tModel9;
                        }
                    }
                }
            }
            else if (tModel9.Duracion_Etapa > durObjetivo)
            {
                while ((tModel9.Duracion_Etapa > durObjetivo))
                {
                    difObjetivo = tModel9.Duracion_Etapa - durObjetivo;
                    if (difObjetivo > 2)
                        prod.PesoFinal -= difObjetivo + 1;
                    else
                        prod.PesoFinal -= 1;

                    tModel9 = GetDataRegistro(prod, objReqDataIni);
                }
                double difObjetivoFinal = durObjetivo - tModel9.Duracion_Etapa;
                if (difObjetivoFinal > 0.5)
                {
                    while ((tModel9.Duracion_Etapa < durObjetivo))
                    {
                        prod.PesoFinal += 0.5;
                        tModel9 = GetDataRegistro(prod, objReqDataIni);
                        if ((int)Math.Round(tModel9.Edad_Final) == edadTopeObjetivo)
                        {
                            return tModel9;
                        }
                    }
                }
            }


            return tModel9;
        }

        private VarMinMaxModel GetMinMaxValues(RequestOptimizerModel objReqDataIni, VarMinMaxModel prodAnt, int identificador)
        {
            double pesoMin = 0;
            double pesoMax = 0;
            double peso2Min = 0;
            double peso2Max = 0;

            double edadTope = objReqDataIni.EdadVenta - objReqDataIni.Productos.FindAll(t => t.CveProducto > identificador).Sum(t => t.DuracionMin);
            ProductoModel prod = objReqDataIni.Productos.Find(r => r.Posicion == identificador);
            double minEtapa = objReqDataIni.Productos.Find(r => r.Posicion == identificador).DuracionMin;
            double maxEtapa = objReqDataIni.Productos.Find(r => r.Posicion == identificador).DuracionMax;
            double maxEtapa2 = maxEtapa;
            Boolean isExactDuracion = false;
            if (minEtapa == maxEtapa)
            {
                isExactDuracion = true;
                maxEtapa += 1;
            }
            double edadFinalMinima = 0;
            double edadFinalMaxima = 0;
            double edadFinal2Minima = 0;
            double edadFinal2Maxima = 0;
            for (var x = prodAnt.ValorMinimo; x <= 200; x++)
            {
                objReqDataIni.IsOptimizar = true;
                objReqDataIni.PesoInicialTmp = prodAnt.ValorMinimo;
                objReqDataIni.EdadInicialTmp = prodAnt.EdadMinAnteriorFinal;
                prod.PesoFinal = x;
                TablaModel tModel = GetDataRegistro(prod, objReqDataIni);
                double duracion = Math.Round(tModel.Duracion_Etapa,0);
                if (duracion >= minEtapa & duracion <= maxEtapa)
                {
                    pesoMax = x;
                    edadFinalMaxima = tModel.Edad_Final;
                    if (pesoMin == 0)
                    {
                        pesoMin = x;
                        edadFinalMinima = tModel.Edad_Final;
                        if (isExactDuracion)
                        {
                            //ajuste de duracion
                            for (var z = x; z >= 0; z -= 0.01)
                            {
                                prod.PesoFinal = z;
                                tModel = GetDataRegistro(prod, objReqDataIni);
                                if (Math.Round(tModel.Duracion_Etapa) < minEtapa || minEtapa==0)
                                    break;
                                pesoMin = z;
                                edadFinalMinima = tModel.Edad_Final;
                            }
                        }
                    }
                    

                }
                if (duracion > maxEtapa | tModel.Edad_Final > edadTope)
                    break;
            }
            if (prodAnt.ValorMaximo > 0)
            {
                for (var x = prodAnt.ValorMaximo; x <= 200; x++)
                {
                    objReqDataIni.IsOptimizar = true;
                    objReqDataIni.PesoInicialTmp = prodAnt.ValorMaximo;
                    objReqDataIni.EdadInicialTmp = prodAnt.EdadMaxAnteriorFinal;
                    prod.PesoFinal = x;
                    TablaModel tModel = GetDataRegistro(prod, objReqDataIni);
                    double duracion = Math.Round(tModel.Duracion_Etapa);
                    if (duracion >= minEtapa & duracion <= maxEtapa)
                    {
                        if (peso2Min == 0)
                        {
                            peso2Min = x;
                            edadFinal2Minima = tModel.Edad_Final;
                        }
                        if (duracion > maxEtapa2 | tModel.Edad_Final > edadTope + 1)
                            break;
                        peso2Max = x;
                        edadFinal2Maxima = tModel.Edad_Final;
                    }
                    if (Math.Round(tModel.Edad_Final) == edadTope )
                    {
                        peso2Max = x;
                        edadFinal2Maxima = tModel.Edad_Final;
                        break;
                    }
                }
                if (edadFinal2Maxima <= edadFinalMinima) edadFinal2Maxima = edadFinalMinima;
                if (peso2Max <= pesoMin) peso2Max = pesoMin;
                return new VarMinMaxModel(pesoMin, peso2Max, edadFinalMinima, edadFinal2Maxima);
            }
            else
                if (edadFinalMaxima <= edadFinalMinima) edadFinalMaxima = edadFinalMinima;
                if (pesoMax <= pesoMin) pesoMax = pesoMin;
            return new VarMinMaxModel(pesoMin, pesoMax, edadFinalMinima, edadFinalMaxima);
        }
        private List<TablaModel> GetEtapasIniciales(RequestOptimizerModel objReqDataIni)
        {
            List<TablaModel> lstProdIni = new List<TablaModel>();
            objReqDataIni.Productos.FindAll(r => r.Posicion < 5).ForEach(p =>
            {
                lstProdIni.Add(GetDataRegistro(p, objReqDataIni));
            }
    );
            return lstProdIni;
        }

        private List<ResponseOptModel> GetDataResult(RequestOptimizerModel objReqDataIni, CatalogoModel dataCat, ref double minVal, ref double maxVal, double pesoFinal5, double EdadIni, double EdadFin, double edadResultante)
        {
            RequestOptimizerModel objReqData = new RequestOptimizerModel();
            objReqData = (RequestOptimizerModel)objReqDataIni.Clone();

            List<ResponseOptModel> lstResultTmp = new List<ResponseOptModel>();
            bool saltaEtapa1 = false;
            bool saltaEtapa2 = false;
            bool saltaEtapa3 = false;
            int pesoFinal10 = int.Parse(dataCat.Valor);
            int pesoFinal9 = int.Parse(dataCat.Clave);

            minVal = objReqData.Productos.Find(k => k.CveProducto == 6).DuracionMin;
            maxVal = objReqData.Productos.Find(k => k.CveProducto == 6).DuracionMax;
            double minVal2 = objReqData.Productos.Find(k => k.CveProducto == 7).DuracionMin;
            double maxVal2 = objReqData.Productos.Find(k => k.CveProducto == 7).DuracionMax;
            double minVal3 = objReqData.Productos.Find(k => k.CveProducto == 8).DuracionMin;
            double maxVal3 = objReqData.Productos.Find(k => k.CveProducto == 8).DuracionMax;
            double minVal4 = objReqData.Productos.Find(k => k.CveProducto == 9).DuracionMin;
            double maxVal4 = objReqData.Productos.Find(k => k.CveProducto == 9).DuracionMax;

            if ((maxVal2 > edadResultante))
                maxVal2 = edadResultante;
            if ((maxVal3 > edadResultante))
                maxVal3 = edadResultante;
            if ((maxVal4 > edadResultante))
                maxVal4 = edadResultante;
            if ((maxVal > edadResultante))
                maxVal = edadResultante;
            for (var x = pesoFinal5 + minVal; x <= pesoFinal5 + maxVal; x++)
            {
                if (x >= pesoFinal5 + minVal & x <= pesoFinal5 + maxVal)
                {
                    objReqData.Productos.Find(k => k.CveProducto == 6).PesoFinal = x;
                    saltaEtapa2 = false;
                    if (saltaEtapa1)
                        break;

                    for (var y = x + minVal2; y <= x + maxVal2; y++)
                    {
                        saltaEtapa3 = false;
                        if (saltaEtapa2)
                            break;
                        if (y >= x + minVal2 & y <= x + maxVal2 & y <= pesoFinal9 - minVal3)
                        {
                            if (minVal3 == 0 & maxVal3 == 0)
                            {
                            }
                            if (minVal2 == 0 & maxVal2 == 0)
                                // x = y
                                objReqData.Productos.Find(k => k.CveProducto == 6).PesoFinal = x;
                            objReqData.Productos.Find(l => l.CveProducto == 7).PesoFinal = y;

                            for (var z = y + minVal3; z <= y + maxVal3; z++)
                            {
                                if (z > pesoFinal9 - minVal4)
                                {
                                    saltaEtapa3 = true;
                                    break;
                                }
                                if (z >= y + minVal3 & z <= y + maxVal3)
                                {
                                    objReqData.Productos.Find(l => l.CveProducto == 8).PesoFinal = z;
                                    objReqData.Productos.Find(l => l.CveProducto == 9).PesoFinal = pesoFinal9;
                                    objReqData.Productos.Find(l => l.CveProducto == 10).PesoFinal = pesoFinal10;

                                    ResponseOptimizerModel objResp = GetDataOpt(objReqData);
                                    if (objResp.Data.FindAll(k => (k.Identificador == 8) & (k.Duracion_Etapa > k.Duracion_Maxima)).Count > 0)
                                    {
                                        // si sudece esto quiere decir que la duración sobre pasa la etapa 8 pero va disminuyendo porqeu va aumentando y
                                        // k.Duracion_Etapa - k.Duracion_Maxima

                                        double difEtapa = (objResp.Data.Find(k => k.Identificador == 8).Duracion_Etapa - maxVal3) * objResp.Data.Find(k => k.Identificador == 8).GDP;
                                        y += Math.Truncate(difEtapa);
                                    }
                                    if (objResp.Data.FindAll(k => (k.Identificador == 7) & (k.Duracion_Etapa > k.Duracion_Maxima)).Count > 0)
                                    {
                                        double difEtapa = (objResp.Data.Find(k => k.Identificador == 7).Duracion_Etapa - maxVal2) * objResp.Data.Find(k => k.Identificador == 7).GDP;
                                        x += Math.Truncate(difEtapa);
                                    }
                                    // If objResp.Data.FindAll(Function(k) (k.Identificador = 6 Or k.Identificador = 7 Or k.Identificador = 8) And (k.Duracion_Etapa < minVal Or k.Duracion_Etapa > maxVal)).Count > 0 Then
                                    // saltaEtapaZ = True
                                    // Exit For
                                    // End If
                                    if (System.Convert.ToDouble(objResp.Data.Find(j => j.Identificador == 10).Edad_Final) < EdadFin)
                                        y += EdadFin - System.Convert.ToDouble(objResp.Data.Find(j => j.Identificador == 10).Edad_Final);
                                    if (System.Convert.ToDouble(objResp.Data.Find(j => j.Identificador == 10).Edad_Final) > EdadFin + 5)
                                    {
                                        // saltaEtapa3 = True
                                        if (System.Convert.ToDouble(objResp.Data.Find(j => j.Identificador == 9).Edad_Final) > EdadIni + 5)
                                            saltaEtapa3 = true;
                                    }
                                    bool bandAplica = false;
                                    if (System.Convert.ToDouble(objResp.Data.Find(j => j.Identificador == 9).Edad_Final) == EdadIni & System.Convert.ToDouble(objResp.Data.Find(j => j.Identificador == 10).Edad_Final) == EdadFin)
                                    {
                                        if (!(objResp.Data.FindAll(k => (k.Identificador == 6 | k.Identificador == 7 | k.Identificador == 8 | k.Identificador == 9) & (k.Duracion_Etapa < k.Duracion_Minima | k.Duracion_Etapa > k.Duracion_Maxima)).Count > 0))
                                            bandAplica = true;
                                    }

                                    if (bandAplica)
                                    {
                                        ResponseOptModel objR = new();
                                        objR.ValorObjetivo = objResp.Optimizer.Find(p => p.Orden == 7).Valor;
                                        objR.LstProducto.Add(new CatalogoModel("5", pesoFinal5.ToString()));
                                        objR.LstProducto.Add(new CatalogoModel("6", objResp.Data.Find(k => k.Identificador == 6).Peso_Final.ToString()));
                                        objR.LstProducto.Add(new CatalogoModel("7", objResp.Data.Find(k => k.Identificador == 7).Peso_Final.ToString()));
                                        objR.LstProducto.Add(new CatalogoModel("8", objResp.Data.Find(k => k.Identificador == 8).Peso_Final.ToString()));
                                        objR.LstProducto.Add(new CatalogoModel("9", pesoFinal9.ToString()));
                                        objR.LstProducto.Add(new CatalogoModel("10", pesoFinal10.ToString()));

                                        lstResultTmp.Add(objR);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return lstResultTmp;
        }

        private RequestOptimizerModel getRequestData(RequestOptimizerModel objReqData, double pF6, double pF7, double pF8, double pf9, double pf10, double EdadFin, double EdadIni)
        {
            RequestOptimizerModel reqTemp = new RequestOptimizerModel();

            reqTemp = (RequestOptimizerModel)objReqData.Clone();

            double minval1 = objReqData.Productos.Find(p => p.CveProducto == 6).DuracionMin;
            double minval2 = objReqData.Productos.Find(p => p.CveProducto == 7).DuracionMin;
            double minval3 = objReqData.Productos.Find(p => p.CveProducto == 8).DuracionMin;
            double minval4 = objReqData.Productos.Find(p => p.CveProducto == 9).DuracionMin;

            double maxval1 = objReqData.Productos.Find(p => p.CveProducto == 6).DuracionMax;
            double maxval2 = objReqData.Productos.Find(p => p.CveProducto == 7).DuracionMax;
            double maxval3 = objReqData.Productos.Find(p => p.CveProducto == 8).DuracionMax;
            double maxval4 = objReqData.Productos.Find(p => p.CveProducto == 9).DuracionMax;

            double ranIni1 = pF6 - 0.1;
            double ranFin1 = pF6 + 0.1;
            double ranIni2 = pF7 - 0.1;
            double ranFin2 = pF7 + 0.1;
            double ranIni3 = pF8 - 0.1;
            double ranFin3 = pF8 + 0.1;
            double ranIni4 = pf9 - 0.1;
            double ranFin4 = pf9 + 0.1;

            for (double x = ranIni1; x <= ranFin1; x += 0.02)
            {
                if (System.Convert.ToInt32(minval1) == 0 & System.Convert.ToInt32(maxval1) == 0)
                    x = objReqData.Productos.Find(p => p.CveProducto == 5).PesoFinal;
                for (double y = ranIni2; y <= ranFin2; y += 0.02)
                {
                    for (double z = ranIni3; z <= ranFin3; z += 0.02)
                    {
                        objReqData.Productos.Find(p => p.CveProducto == 8).PesoFinal = z;
                        if (System.Convert.ToInt32(minval3) == 0 & System.Convert.ToInt32(maxval3) == 0)
                            objReqData.Productos.Find(p => p.CveProducto == 8).PesoFinal = y;
                        objReqData.Productos.Find(p => p.CveProducto == 6).PesoFinal = x;
                        if (System.Convert.ToInt32(minval2) == 0 & System.Convert.ToInt32(maxval2) == 0)
                            objReqData.Productos.Find(p => p.CveProducto == 6).PesoFinal = y;

                        objReqData.Productos.Find(p => p.CveProducto == 7).PesoFinal = y;
                        objReqData.Productos.Find(p => p.CveProducto == 9).PesoFinal = pf9;
                        ResponseOptimizerModel objRespOptTmp = GetDataOptizerService(objReqData);
                        // Reviso etapa 8 
                        TablaModel etapa8 = objRespOptTmp.Data.Find(k => (k.Identificador == 8));
                        TablaModel etapa7 = objRespOptTmp.Data.Find(k => (k.Identificador == 7));
                        double dirEtapaDur = etapa8.Duracion_Minima * 100 - etapa8.Duracion_Etapa * 100;
                        if (dirEtapaDur > 5)
                            z += (dirEtapaDur - 5) / (double)100;
                        double dirEtapaDur2 = etapa7.Duracion_Minima * 100 - etapa7.Duracion_Etapa * 100;
                        if (dirEtapaDur2 > 5)
                            y += (dirEtapaDur2 - 5) / (double)100;

                        double difEdad = Math.Abs(System.Convert.ToDouble(objRespOptTmp.Data.Find(j => j.Identificador == 9).Edad_Final * 100) - EdadFin * 100);
                        if (difEdad > 15)
                            break;
                        if (System.Convert.ToDouble(objRespOptTmp.Data.Find(j => j.Identificador == 9).Edad_Final * 10) == EdadFin * 10 & !(objRespOptTmp.Data.FindAll(k => (k.Identificador == 6 | k.Identificador == 7 | k.Identificador == 8) & (k.Duracion_Etapa < k.Duracion_Minima | k.Duracion_Etapa > k.Duracion_Maxima)).Count > 0))
                            return objReqData;
                        if (System.Convert.ToInt32(minval3) == 0 & System.Convert.ToInt32(maxval3) == 0)
                            break;
                    }
                }
                if (System.Convert.ToInt32(minval1) == 0 & System.Convert.ToInt32(maxval1) == 0)
                    break;
            }
            return reqTemp;
        }

        private void SaveData(RespOptimizerModel objResp, RequestOptimizerModel objReq, int cveParametroSeleccionado)
        {

            string strSQLParam = "DELETE [OptimizerC_PlanA_Resultado] WHERE CvePlan =" + objReq.CvePlan.ToString();
            Database.execNonQuery(strSQLParam);

            strSQLParam = "INSERT INTO [OptimizerC_PlanA_Resultado](CvePlan,  Request,Response,FecAct,UsuAct) ";
            string jsonResp = JsonConvert.SerializeObject(objResp);
            string jsonReq = JsonConvert.SerializeObject(objReq);
            strSQLParam += "VALUES(" + objReq.CvePlan.ToString() + ",'" + jsonReq + "','" + jsonResp + "',GETDATE(),'" + objReq.UsuAct + "') ";
            Database.execNonQuery(strSQLParam);

            SaveDataReporte(objResp, objReq, cveParametroSeleccionado);

        }

        private void SaveDataReporte(RespOptimizerModel objResp, RequestOptimizerModel objReq, int cveParametroSeleccionado)
        {
            string strSQLParam = "DELETE [OptimizerC_PlanA_Reporte] WHERE CvePlan =" + objReq.CvePlan.ToString();
            Database.execNonQuery(strSQLParam);

            objResp.ResponseParametro.ForEach(resp =>
            {
                strSQLParam = "INSERT INTO [OptimizerC_PlanA_Reporte](CvePlan,  CveParametro,Seleccionado,CDA,Presupuesto,GDP,CA,DuracionTotal,PrecioVenta,PesoVenta,EdadVenta,KilosProducidos,Costo_Ponderado,Costo_TotalAlimento,Costo_KiloProducido,Costo, Utilidad,ROI ";
                strSQLParam += ", Presupuesto_P1, Presupuesto_P2, Presupuesto_P3, Presupuesto_P4, Presupuesto_P5, Presupuesto_P6, Presupuesto_P7, Presupuesto_P8, Presupuesto_P9, Presupuesto_P10) ";

                strSQLParam += "VALUES(" + objReq.CvePlan.ToString() + ",'" + resp.CveParametro + "','" + (cveParametroSeleccionado == resp.Resultado.CveParametro ? 1 : 2) + "','" + resp.Resultado.Cda.ToString() + "','" + resp.Resultado.Presupuesto.ToString() + "','" + resp.Resultado.Gdp.ToString() + "','" + resp.Resultado.Ca.ToString() + "','" + resp.Resultado.DuracionTotal.ToString() + "','" + resp.Resultado.PrecioVenta.ToString() + "','" + resp.Resultado.PesoVenta.ToString() + "','" + resp.Resultado.EdadVenta.ToString() + "','" + resp.Resultado.KilosProducidos.ToString() + "','" + resp.Resultado.CostoPonderado.ToString() + "','" + resp.Resultado.CostoTotalAlimento.ToString() + "','" + resp.Resultado.CostoKiloProducido.ToString() + "','" + resp.Resultado.Costo.ToString() + "','" + resp.Resultado.Utilidad.ToString() + "','" + resp.Resultado.Roi.ToString() + "'";
                for (int i = 1; i < 11; i++)
                {
                    double valPresupuesto = 0.0;
                    EtapaResModel presupuestoEtapa = resp.Resultado.Presupuestos.Find(pr => pr.Clave == i);
                    if (presupuestoEtapa != null)
                    {
                        valPresupuesto = presupuestoEtapa.Valor;
                    }
                    strSQLParam += ",'" + valPresupuesto + "'";
                }
                ;

                strSQLParam += ") ";

                Database.execNonQuery(strSQLParam);
            }
            );


        }

        [HttpPost]
        [Route("api/optimizer")]
        public ResponseOptimizerModel GetDataOptizerService([FromBody] RequestOptimizerModel objReqData)
        {
            string json = JsonConvert.SerializeObject(objReqData);
            string strSQLRef = "SELECT * FROM CatOptimizerC_Referencias ";
            dtRef = Database.execQuery(strSQLRef);

            string strSQLConst = "SELECT * FROM CatOptimizerC_Constantes ";
            dtConst = Database.execQuery(strSQLConst);

            string strSQLParam = "SELECT * FROM CatOptimizerC_ParametrosEconomicos ";
            dtParam = Database.execQuery(strSQLParam);

            return GetDataOpt(objReqData);
        }
        public ResponseOptimizerModel GetDataOpt(RequestOptimizerModel objReqData)
        {
            this.objReq = objReqData;
            List<TablaModel> tablaOpt = GetTablaOptimizer(objReqData);

            ResponseOptimizerModel respCal = new ResponseOptimizerModel()
            {
                Data = tablaOpt,
                Optimizer = GetOptimizer(objReq, tablaOpt)
            };

            respCal.Resultado = GetResultado(objReq, tablaOpt, respCal.Optimizer);
            return respCal;
        }

        private ResultadoOptimizerModel GetResultado(RequestOptimizerModel objReq, List<TablaModel> tablaOpt, List<OptimizerModel> optimizer)
        {

            ResultadoOptimizerModel resultado = new ResultadoOptimizerModel();
            resultado.CveParametro = objReq.CveParametro;

            List<TablaModel> tablaFilter = tablaOpt.FindAll(t => t.Costo > 0 && t.Duracion_Etapa > 0);
            int numRegistros = tablaFilter.Count;

            resultado.KilosProducidos = optimizer.Find(o => o.Orden == 0).Valor;

            resultado.PrecioVenta = objReq.PrecioVenta;
            resultado.Presupuesto = tablaFilter.Sum(t => t.PresupuestoCerdo);
            resultado.Ca = resultado.Presupuesto / resultado.KilosProducidos;
            resultado.DuracionTotal = tablaFilter.Sum(t => t.Duracion_Etapa);
            resultado.Gdp = resultado.KilosProducidos / resultado.DuracionTotal;
            resultado.Cda = resultado.Presupuesto / resultado.DuracionTotal;
            resultado.Costo = tablaFilter.Sum(t => t.Costo);



            resultado.EdadVenta = tablaFilter.Max(t => t.Edad_Final); //objReq.EdadVenta ;
            resultado.PesoVenta = tablaFilter.Max(t => t.Peso_Final);

            resultado.CostoPonderado = optimizer.Find(o => o.Orden == 2).Valor;
            resultado.CostoTotalAlimento = optimizer.Find(o => o.Orden == 3).Valor;
            resultado.CostoKiloProducido = optimizer.Find(o => o.Orden == 4).Valor;
            resultado.Utilidad = optimizer.Find(o => o.Orden == 5).Valor;
            resultado.Roi = optimizer.Find(o => o.Orden == 6).Valor;

            tablaFilter.ForEach(t => resultado.Presupuestos.Add(new EtapaResModel(t.Identificador, t.PresupuestoCerdo)));




            return resultado;
        }

        public List<OptimizerModel> GetOptimizer(RequestOptimizerModel objReq, List<TablaModel> tablaOpt)
        {
            List<OptimizerModel> objResp = new List<OptimizerModel>();
            int idMin = tablaOpt.Min(p => System.Convert.ToInt32(p.Identificador));
            int idMax = tablaOpt.Max(p => System.Convert.ToInt32(p.Identificador));

            double pesoInicial = tablaOpt.Find(p => System.Convert.ToInt32(p.Identificador) == idMin).Peso_Inicial;
            double pesoFinal = tablaOpt.Find(p => System.Convert.ToInt32(p.Identificador) == idMax).Peso_Final;
            double kilosProducidos = pesoFinal - pesoInicial;
            objResp.Add(new OptimizerModel("Kilos producidos", 0, kilosProducidos));
            double valorKilosProd = objReq.PrecioVenta * kilosProducidos;
            objResp.Add(new OptimizerModel(GetParametroEcoVal(1, "NomParametro"), short.Parse(GetParametroEcoVal(1, "Posicion")), valorKilosProd));
            double sumProductoCostoxPresupuesto = tablaOpt.Sum(p => p.Costo * p.PresupuestoCerdo);
            double sumPresupuesto = tablaOpt.Sum(p => p.PresupuestoCerdo);
            double costoPonderado = sumProductoCostoxPresupuesto / sumPresupuesto;
            objResp.Add(new OptimizerModel(GetParametroEcoVal(2, "NomParametro"), short.Parse(GetParametroEcoVal(2, "Posicion")), costoPonderado));

            objResp.Add(new OptimizerModel(GetParametroEcoVal(3, "NomParametro"), short.Parse(GetParametroEcoVal(3, "Posicion")), sumProductoCostoxPresupuesto));
            objResp.Add(new OptimizerModel(GetParametroEcoVal(4, "NomParametro"), short.Parse(GetParametroEcoVal(4, "Posicion")), costoPonderado * (sumPresupuesto / kilosProducidos)));
            double utilidadAlimento = valorKilosProd - sumProductoCostoxPresupuesto;
            objResp.Add(new OptimizerModel(GetParametroEcoVal(5, "NomParametro"), short.Parse(GetParametroEcoVal(5, "Posicion")), utilidadAlimento));
            objResp.Add(new OptimizerModel(GetParametroEcoVal(6, "NomParametro"), short.Parse(GetParametroEcoVal(6, "Posicion")), (utilidadAlimento / sumProductoCostoxPresupuesto) * 100));
            // Dim paraminteres As Double = 0
            // Select Case objReq.CveParametro
            // Case 1
            // paraminteres = valorKilosProd
            // Case 2
            // paraminteres = costoPonderado
            // Case 3
            // paraminteres = sumProductoCostoxPresupuesto
            // Case 4
            // paraminteres = costoPonderado * (sumPresupuesto / kilosProducidos)
            // Case 5
            // paraminteres = utilidadAlimento
            // Case 6
            // paraminteres = (utilidadAlimento / sumProductoCostoxPresupuesto) * 100
            // Case Else
            // paraminteres = utilidadAlimento
            // End Select

            // objResp.Add(New OptimizerModel("Parámetro económico de interés", 7, paraminteres))
            return objResp;
        }



        public List<TablaModel> GetTablaOptimizer(RequestOptimizerModel reqModel)
        {
            List<TablaModel> data = new List<TablaModel>();
            try
            {
                reqModel.Productos.ForEach(p =>
                {
                    TablaModel prod = GetDataRegistro(p, reqModel);
                    if (prod.Identificador == 5 & objReq.IsOptimizar & System.Convert.ToInt32(prod.Edad_Final) < System.Convert.ToInt32(reqModel.EdadSalida))
                        throw new Exception("Edad final <> Edad Salida");
                    data.Add(prod);
                });
                return data;
            }
            catch (Exception ex)
            {
                return data;
            }
        }

        public List<CatalogoModel> GetTablaOptimizerxProd(RequestOptimizerModel reqModel, int Identificador, int rango5 = 0)
        {
            TablaModel result = new TablaModel();
            objReq = reqModel;
            double minVal = Math.Round(objReq.EdadVenta - objReq.EdadSalida - objReq.DiasRactopamina);
            double EdadIni = reqModel.EdadVenta - reqModel.DiasRactopamina;
            double EdadFin = reqModel.EdadVenta;
            reqModel.EdadInicialTmp = EdadIni;
            reqModel.EdadFinalTmp = EdadFin;
            List<CatalogoModel> datosPesos = new List<CatalogoModel>();
            double minEtapa9 = reqModel.Productos.Find(r => r.Posicion == 9).DuracionMin;
            double maxEtapa9 = reqModel.Productos.Find(r => r.Posicion == 9).DuracionMax;
            reqModel.Productos.FindAll(r => r.Posicion == Identificador).ForEach(p =>
            {
                double RangoIni = 0;
                if (Identificador == 10)
                    RangoIni = minVal + minEtapa9;
                for (var x = RangoIni; x <= 150; x++)
                {
                    p.PesoFinal = x;
                    reqModel.IsOptimizar = true;
                    reqModel.PesoInicialTmp = x;
                    for (var n = x + reqModel.DiasRactopamina - 5; n <= 150; n++)
                    {
                        p.PesoFinal = n;
                        TablaModel prod = null/* TODO Change to default(_) if this is not a reference type */;
                        prod = GetDataRegistro(p, reqModel, prod);
                        if (System.Convert.ToDouble(prod.Edad_Final) == EdadFin)
                            datosPesos.Add(new CatalogoModel(prod.Peso_Inicial.ToString(), prod.Peso_Final.ToString()));
                        if (System.Convert.ToDouble(prod.Edad_Final) > EdadFin)
                            break;
                    }
                    reqModel.IsOptimizar = false;
                }
            });

            return datosPesos;
        }

        public TablaModel GetTablaOptimizerxProd5(RequestOptimizerModel reqModel, int Identificador, List<TablaModel> lstProdIni)
        {
            TablaModel result = new TablaModel();
            objReq = reqModel;
            List<CatalogoModel> datosPesos = new List<CatalogoModel>();


            reqModel.Productos.FindAll(r => r.Posicion == Identificador).ForEach(p =>
            {
                TablaModel etapa4 = lstProdIni.Find(e => e.Identificador == 4);
                objReq.IsOptimizar = true;
                objReq.PesoInicialTmp = etapa4.Peso_Final;
                objReq.EdadInicialTmp = etapa4.Edad_Final;
                double RangoIni = etapa4.Peso_Final;
                for (var x = RangoIni; x <= 200; x++)
                {
                    p.PesoFinal = x;
                    TablaModel prod = GetDataRegistro(p, reqModel);
                    if (prod.Edad_Final >= reqModel.EdadSalida)
                    {
                        // si ya se pasó ajusto para que da exacta la cantidad
                        double RangoIniDet = x;

                        for (var y = RangoIniDet - 1; y <= RangoIniDet; y += 0.01)
                        {
                            p.PesoFinal = y;
                            TablaModel prodDet = GetDataRegistro(p, reqModel);
                            if (Math.Round(prodDet.Edad_Final, 2) >= Math.Round(reqModel.EdadSalida, 2))
                            {
                                result = prodDet;
                                return;
                            }
                        }
                    }
                }
            });

            return result;
        }
        public TablaModel GetDataRegistro(ProductoModel prod, RequestOptimizerModel reqModel, TablaModel? registroTmp = null)

        {
            TablaModel registro = new TablaModel();
            int Identificador = prod.CveProducto;
            if (registroTmp != null)
                registro = registroTmp;
            else
            {
                registro.Identificador = Identificador;
                registro.Costo = prod.Costo;
                if (prod.IsEtapa.ToUpper() == "S")
                {
                    registro.Ractopamina = prod.Ractopamina;
                    registro.SIDLys_GDP = GetSID_GDP(Identificador);
                    registro.EM_Mixto = GetEM_Mixto(Identificador);
                    registro.EM_KCal = prod.EM; // GetDataNoNUPIG(Identificador, "EMKcalKg")
                    registro.En_Kcal = prod.EN;  // GetDataNoNUPIG(Identificador, "ENKcalKg")
                    registro.EN_EM = GetEN_EM(Identificador);
                    registro.SIDLysPorc = prod.SID; // GetDataNoNUPIG(Identificador, "SIDLys")
                }

                registro.CDA_TA = GetCDA_TA(Identificador);
                registro.CDA_Espacio = GetCDA_Espacio(Identificador);
                registro.Max_Energia = GetMax_Energia(Identificador);
                registro.Min_Espacio = GetMin_Espacio(Identificador);
                registro.TC_min = GetTC_Min(Identificador);
                registro.GDP_Curva = GetGDP_Curva(Identificador);
                registro.GDP = GetGDP(Identificador);

                registro.PresupuestoCerdo = GetPresupuesto_Cerdo(Identificador);
                registro.CDA_Kg = GetCDA_Kg(Identificador);
                registro.Ajuste_Temp = GetAjuste_T(Identificador);
            }


            registro.Peso_Inicial = GetPeso_Inicial(Identificador);
            registro.Peso_Final = GetPeso_Final(Identificador);
            registro.CA = GetCA(Identificador);
            registro.CA_GDP = GetCA_GDP(Identificador);
            registro.Edad_Inicial = GetEdad_Inicial(Identificador);
            registro.Edad_Final = GetEdad_Final(Identificador);
            registro.Duracion_Etapa = GetDuracion_Etapa(Identificador);
            registro.Duracion_Minima = GetDuracion_Min(Identificador);
            registro.Duracion_Maxima = GetDuracion_Max(Identificador);
            return registro;
        }
        // Public Function GetCosto(Identificador) As Double
        // If Identificador.Contains("NUPIG") Then
        // Return objReq.Productos.Find(Function(p) p.NomProducto = Identificador).Presupuesto 'objReq.PresupuestoCerdo.Find(Function(p) p.Nombre = Identificador).Precio
        // Else
        // Return objReq.Productos.Find(Function(p) p.NomProducto = Identificador).Presupuesto 'objReq.EnergiaMeta.Find(Function(p) p.Nombre = Identificador).Precio
        // End If

        // End Function
        public double GetEN_EM(int Identificador)
        {
            // =H11/G11
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);

            double H11 = prod.EN; // GetDataNoNUPIG(Identificador, "ENKcalKg")
            double G11 = prod.EM; // GetDataNoNUPIG(Identificador, "EMKcalKg")
            if (G11 == 0)
                return 0;
            return H11 / G11;
        }
        public double GetSID_GDP(int Identificador)
        {
            // =_xlfn.IFS($AA$25=$AS$1,$AI$8*PROMEDIO(U11:V11)^2+$AI$9*PROMEDIO(U11:V11)+$AI$10,$AA$25=AS$2,$AH$8*PROMEDIO(U11:V11)^2+$AH$9*PROMEDIO(U11:V11)+$AH$10)
            int AA25 = objReq.CveEstado;  // objReq.ParametroVal.Find(Function(p) p.Nombre = "Estado sanitario").ValorTexto
            int AS1 = 1;
            double AI8 = -0.000328785619892022;
            double AI9 = 0.0537337247137016;
            double AI10 = 17.751865855144;
            double AH8 = -0.00116689320927865;
            double AH9 = 0.134553033627026;
            double AH10 = 18.033660273519;

            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.Gdp > 0)
            {
                return prod.Gdp;
            }

            double U11 = GetPeso_Inicial(Identificador);
            double V11 = GetPeso_Final(Identificador);
            double Promedio = (U11 + V11) / 2;

            if (prod.Ractopamina > 0)
            {
                // =($DM$21*(V15-U15)^3+$DM$22*(V15-U15)^2+$DM$23*(V15-U15)+$DM$24)
                double DM21 = 0.000104974155529151;
                double DM22 = -0.00899246399250125;
                double DM23 = 0.209152449591525;
                double DM24 = 22.6486394101781;

                return (DM21 * Math.Pow((V11 - U11), 3) + DM22 * Math.Pow((V11 - U11), 2) + DM23 * (V11 - U11) + DM24);
            }
            if (AA25 == AS1)
                // $AI$8*PROMEDIO(U11:V11)^2+$AI$9*PROMEDIO(U11:V11)+$AI$10
                return AI8 * Math.Pow(Promedio, 2) + AI9 * Promedio + AI10;
            else
                // $AH$8*PROMEDIO(U11:V11)^2+$AH$9*PROMEDIO(U11:V11)+$AH$10
                return AH8 * Math.Pow(Promedio, 2) + AH9 * Promedio + AH10;
        }
        public double GetEM_Mixto(int Identificador)
        {
            // =(1-((-0.191263+(0.019013*(V11-U11))-(0.000443*(V11-U11)^2)+(0.000003539*(V11-U11)^3))*(D11/20)^0.7))*(10563*(1-EXP(-EXP(-4.04)*PROMEDIO(U11:V11))))
            double U11 = GetPeso_Inicial(Identificador);
            double V11 = GetPeso_Final(Identificador);
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            double D11 = prod.Ractopamina;
            double Promedio = (U11 + V11) / 2;
            return (1 - ((-0.191263 + (0.019013 * (V11 - U11)) - (0.000443 * Math.Pow((V11 - U11), 2)) + (0.000003539 * Math.Pow((V11 - U11), 3))) * Math.Pow((D11 / 20), 0.7))) * (10563 * (1 - Math.Exp(-Math.Exp(-4.04) * Promedio)));
        }
        public double GetDataNoNUPIG(int Identificador, string Elemento)
        {
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa == "S")
            {
                switch (Elemento)
                {
                    case "Ractopamina":
                        {
                            return objReq.Productos.Find(p => p.CveProducto == Identificador).Ractopamina;  // EnergiaMeta.Find(Function(p) p.Nombre = Identificador).Ractopamina
                        }

                    case "EMKcalKg":
                        {
                            return objReq.Productos.Find(p => p.CveProducto == Identificador).EM;
                        }

                    case "ENKcalKg":
                        {
                            return objReq.Productos.Find(p => p.CveProducto == Identificador).EN;
                        }

                    case "SIDLys":
                        {
                            return objReq.Productos.Find(p => p.CveProducto == Identificador).SID;
                        }

                    default:
                        {
                            return 0;
                        }
                }
            }
            else
                return default(Double);
        }


        public double GetCDA_TA(int Identificador)
        {

            // =SI(P7>=1,($AR$21*PROMEDIO(U7:V7)^2+$AR$22*PROMEDIO(U7:V7)+$AR$23),($AR$21*PROMEDIO(U7:V7)^2+$AR$22*PROMEDIO(U7:V7)+$AR$23)*P7)
            double AR21 = -0.00158368094513782;
            double AR22 = 0.0933595380205574;
            double AR23 = -0.342184717875434;
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            double P7 = GetAjuste_T(Identificador);
            if (prod.IsEtapa != "S")
            {
                double Promedio = (GetPeso_Inicial(Identificador) + GetPeso_Final(Identificador)) / 2;
                double result = AR21 * Math.Pow(Promedio, 2) + AR22 * Promedio + AR23;
                if (P7 >= 1)
                    return result;
                else
                    return result * P7;
            }
            else
            {
                // =SI(P11>=1,(F11/G11),(F11/G11)*P11)
                double F11 = GetEM_Mixto(Identificador);
                double G11 = prod.EM;
                if (G11 == 0)
                    return 0;
                if (P7 >= 1)
                    return F11 / G11;
                else
                {
                    double P11 = GetAjuste_T(Identificador);
                    return (F11 / G11) * P11;
                }
            }
        }

        public double GetCDA_Espacio(int Identificador)
        {
            // =SI(N7<0,(K7+(K7*0.252*N7)),(K7))
            double N7 = GetMin_Espacio(Identificador);
            double CDA = GetCDA_TA(Identificador);
            if (N7 < 0)
                return CDA + (CDA * 0.252 * N7);
            else
                return CDA;
        }

        public double GetMax_Energia(int Identificador)
        {
            // =0.0336*V7^0.667
            return 0.0336 * Math.Pow(GetPeso_Final(Identificador), 0.667);
        }

        public double GetMin_Espacio(int Identificador)
        {
            // =(V$26/M7)-1
            return GetMetros_Cerdo() / GetMax_Energia(Identificador) - 1;
        }
        public double GetMetros_Cerdo()
        {
            // =((V21*V20)-(V22*V23)-(V24*V25))/V19
            return objReq.MetrosCerdos;
        }
        public double GetTC_Min(int Identificador)
        {
            // =$AL$21*PROMEDIO(U7:V7)^2+$AL$22*PROMEDIO(U7:V7)+$AL$23
            double AL21 = 0.00106867292160645;
            double AL22 = -0.22906950555408;
            double AL23 = 27.7455381324993;
            double Promedio = (GetPeso_Inicial(Identificador) + GetPeso_Final(Identificador)) / 2;
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa != "S")
                return AL21 * Math.Pow(Promedio, 2) + AL22 * Promedio + AL23;
            else
                // =((3+(17.9-0.0375*PROMEDIO(U11:V11)))+($AL$21*PROMEDIO(U11:V11)^2+$AL$22*PROMEDIO(U11:V11)+$AL$23))/2
                if (Identificador > 5)
                return 3 + (17.9 - 0.0375 * Promedio);
            else
                return ((3 + (17.9 - 0.0375 * Promedio)) + (AL21 * Math.Pow(Promedio, 2) + AL22 * Promedio + AL23)) / 2;
        }

        public double GetAjuste_T(int Identificador)
        {
            // =1-0.012914*($V$18-($O7+3))-0.001179*($V$18-($O7+3))^2
            double V18 = objReq.Temperatura;  // objReq.ParametroVal.Find(Function(p) p.Nombre = "TA C(PROMEDIO - DIA)").Valor
            double O7 = GetTC_Min(Identificador);
            return 1 - 0.012914 * (V18 - (O7 + 3)) - 0.001179 * Math.Pow((V18 - (O7 + 3)), 2);
        }
        public double GetCDA_Kg(int Identificador)
        {
            // =SI(AA7>0,R7/AA7,0)
            double AA7 = GetDuracion_Etapa(Identificador);
            double R7 = GetPresupuesto_Cerdo(Identificador);
            if (AA7 > 0)
                return R7 / AA7;
            else
                return 0;
        }
        public double GetPresupuesto_Cerdo(int Identificador)
        {
            // ='MODELO Requerimiento Nutrientes'!G30
            // respCal.Variables.Find(Function(p) p.Etapas)
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa != "S")
                // ='MODELO Requerimiento Nutrientes'!G30
                return prod.Presupuesto;
            else
            {
                // Return objReq.Productos.Find(Function(p) p.NomProducto = Identificador).PesoFinal 'objReq.PresupuestoCerdo.Find(Function(p) p.Nombre = Identificador).Peso
                // =(L11*AA11)/(1-$C$19)
                double L11 = GetCDA_Espacio(Identificador);
                double AA11 = GetDuracion_Etapa(Identificador);
                double C19 = objReq.Desperdicio / (double)100;
                return (L11 * AA11) / (1 - C19);
            }
        }
        public double GetGDP(int Identificador)
        {
            // =L10/W10
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            double L10 = GetCDA_Espacio(Identificador);
            double W10 = GetCA_GDP(Identificador);

            if (prod.Ractopamina > 0)
            {
                // =L15/W15
                if (W10 == 0)
                    return 0;
                return L10 / W10;
            }
            if (prod.IsEtapa != "S")
                return L10 / W10;
            else
            {
                double T10 = GetGDP_Curva(Identificador);

                // =SI(T11>L11/W11,L11/W11,T11)
                if (T10 > (L10 / W10))
                    return L10 / W10;
                else
                    return T10;
            }
        }
        public double GetGDP_Curva(int Identificador)
        {
            // =L10/W10
            // respCal.Variables.Find(Function(p) p.Etapas)

            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa != "S")
                return 0;
            ResponseDataModel variable = new ResponseDataModel();
            double pesoInicial = GetPeso_Inicial(Identificador);
            double pesoFinal = GetPeso_Final(Identificador);

            double valor = 0;

            double promedio = GetPromedio(pesoInicial, pesoFinal);

            if (objReq.CveReferencia == 5 | objReq.CveReferencia == 6)
            {
                int ref1 = 1;
                int ref2 = 3;
                if (objReq.CveReferencia == 6)
                {
                    ref1 = 2;
                    ref2 = 4;
                }

                double varA1 = GetReferencias(ref1.ToString(), "valorA");
                double varB1 = GetReferencias(ref1.ToString(), "valorB");
                double varC1 = GetReferencias(ref1.ToString(), "valorC");

                double varA2 = GetReferencias(ref2.ToString(), "valorA");
                double varB2 = GetReferencias(ref2.ToString(), "valorB");
                double varC2 = GetReferencias(ref2.ToString(), "valorC");

                double valor1R = varA1 * Math.Pow(promedio, 2) + varB1 * promedio + varC1;
                double valor2R = varA2 * Math.Pow(promedio, 2) + varB2 * promedio + varC2;

                valor = GetPromedio(valor1R, valor2R);
            }
            else
            {

                // =$V$7*PROMEDIO(F5F6)^2+$V$8*PROMEDIO(F5:F6)+$V$9
                double varA = GetReferencias(objReq.CveReferencia.ToString(), "valorA");
                double varB = GetReferencias(objReq.CveReferencia.ToString(), "valorB");
                double varC = GetReferencias(objReq.CveReferencia.ToString(), "valorC");

                valor = varA * Math.Pow(promedio, 2) + varB * promedio + varC;
            }
            if (prod.Ractopamina > 0)
            {
                // +((-0.1*C7^2 + 4.5*C7 + 80)/1000)
                // valor += ((-0.1 * Math.Pow(objReq.PPMRAC, 2) + 4.5 * objReq.PPMRAC + 80) / 1000)
                double c1 = GetConstantes(objReq.CveReferencia, 4, 1);
                double c2 = GetConstantes(objReq.CveReferencia, 4, 2);
                double c3 = GetConstantes(objReq.CveReferencia, 4, 3);
                double c4 = GetConstantes(objReq.CveReferencia, 4, 4);

                valor += ((c1 * Math.Pow(prod.Ractopamina, 2) + c2 * prod.Ractopamina + c3) / c4);
                valor += 0.11;
            }
            return valor;


        }

        public double GetPeso_Inicial(int Identificador)
        {
            // =AA20
            if (objReq.IsOptimizar)
                return objReq.PesoInicialTmp;
            else if (Identificador == ID_NUPIG_SEW)
                return objReq.PesoPromedio; // objReq.ParametroVal.Find(Function(p) p.Nombre = "Peso promedio al destete").Valor
            else
                return GetPeso_Final(Identificador - 1);
        }
        public double GetPeso_Final(int Identificador)
        {
            // =U7+(R7/W7)

            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa == "S")
                return prod.PesoFinal;
            else
                return GetPeso_Inicial(Identificador) + (GetPresupuesto_Cerdo(Identificador) / GetCA(Identificador));
        }

        public double GetCA(int Identificador)
        {
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa == "S")
            {
                // =R11/(V11-U11)
                double R11 = GetPresupuesto_Cerdo(Identificador);
                double V11 = GetPeso_Final(Identificador);
                double U11 = GetPeso_Inicial(Identificador);
                if ((V11 - U11) == 0)
                    return 0;
                return R11 / (V11 - U11);
            }
            else
                return prod.CA;
        }
        public double GetCA_GDP(int Identificador)
        {
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa == "S")
            {
                // =L11/((L11*J11*10)/E11)
                double L11 = GetCDA_Espacio(Identificador);
                double E11 = GetSID_GDP(Identificador);
                double J11 = prod.SID;
                double GDP = L11 / ((L11 * J11 * 10) / E11);
                if (!Information.IsNumeric(GDP))
                    GDP = 0;
                return GDP;
            }
            else
                return prod.CA;
        }

        public double GetEdad_Inicial(int Identificador)
        {
            if (objReq.IsOptimizar)
                return objReq.EdadInicialTmp;
            else if (Identificador == ID_NUPIG_SEW)
                return objReq.EdadDestete;  // objReq.ParametroVal.Find(Function(p) p.Nombre = "Edad al destete").Valor
            else
            {
                int identificadorAnterior = ID_NUPIG_DOS;
                if (Identificador == ID_NUPIG_UNO)
                    identificadorAnterior = ID_NUPIG_SEW;
                if (Identificador == ID_NUPIG_DOS)
                    identificadorAnterior = ID_NUPIG_UNO;
                if (Identificador == ID_NUPIG_TRES)
                    identificadorAnterior = ID_NUPIG_DOS;

                return GetEdad_Final(Identificador - 1);
            }
        }
        public double GetEdad_Final(int Identificador)
        {
            // =Y7+((V7-U7)/S7)

            double Y7 = GetEdad_Inicial(Identificador);
            double V7 = GetPeso_Final(Identificador);
            double U7 = GetPeso_Inicial(Identificador);
            double S7 = GetGDP(Identificador);
            if (S7 > 0)
                return Y7 + ((V7 - U7) / S7);
            else
                return Y7;
        }
        public double GetDuracion_Etapa(int Identificador)
        {
            // =Z7-Y7
            double Z7 = GetEdad_Final(Identificador);
            double Y7 = GetEdad_Inicial(Identificador);
            return Z7 - Y7;
        }
        public double GetDuracion_Min(int Identificador)
        {
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa != "S")
                return 0;
            else
            {
                // =REDOND.MULT(((AA23-AA22-AA24)/3)-14,14)
                if (prod.CveProducto < 11)
                    return prod.DuracionMin;
                return Math.Round((((objReq.EdadVenta - objReq.EdadSalida - objReq.DiasRactopamina) / (double)3) - 14) / (double)14) * 14;
            }
        }
        public double GetDuracion_Max(int Identificador)
        {
            ProductoModel prod = objReq.Productos.Find(p => p.CveProducto == Identificador);
            if (prod.IsEtapa != "S")
                return 0;
            else
            {
                // =REDOND.MULT(((AA23-AA22-AA24)/3)+21,7)
                return prod.DuracionMax;
            }
        }

        public double GetPromedioLst(List<double> datos)
        {
            return datos.Average();
        }
        public double GetPromedio(double val1, double val2)
        {
            List<double> datos = new List<double>();
            datos.Add(val1);
            datos.Add(val2);
            return datos.Average();
        }
        public double GetReferencias(string cveReferencia, string cveColumna)
        {
            if (dtRef != null)
            {
                foreach (DataRow dtR in dtRef.Rows)
                {
                    if (dtR["CveReferencia"].ToString().Equals(cveReferencia))
                    {
                        return double.Parse((string)dtR[cveColumna]);
                    }

                }
            }
            return 0;
        }
        public double GetConstantes(int cveReferencia, int cveVariable, int cveConstante)
        {
            foreach (DataRow dtR in dtConst.Rows)
            {
                if (dtR["CveReferencia"].ToString().Equals(cveReferencia.ToString()) & dtR["CveVariable"].ToString().Equals(cveVariable.ToString()) & dtR["CveConstante"].ToString().Equals(cveConstante.ToString()))
                {
                    return double.Parse((string)dtR["Valor"].ToString());
                }
            }
            return 0;
        }
        public string GetParametroEcoVal(int param, string column)
        {
            if (dtParam != null)
            {
                foreach (DataRow dtR in dtParam.Rows)
                {
                    if (dtR["CveParametro"].Equals(param))
                    {
                        string? v = dtR[column].ToString();
                        return v;
                    }
                }
            }
            return "0";
        }
    }

}
