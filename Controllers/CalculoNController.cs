using System.Data;
using WSOptimizer7.App_Data;
using WSOptimizer7.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace WSOptimizer7.Controllers
{
public class CalculoNController : Controller
    {
        public double dataPesoInicial = 12;
        public double dataEdadInicial = 39;
        public double intervalo = 0.25;
        public double pesoLimite = 20;
        public DataTable dtRef;
        public DataTable dtVar;
        public DataTable dtConst;
        public DataTable dtParam;


        [HttpPost]
        [Route("api/data")]
        public ResponseModel GetCalculo([FromBody]  RequestModel objReq)
        {
            // Dim objReq As New RequestModel
            // objReq.Sexo = "mixto"
            // objReq.Temperatura = "20.0"
            // objReq.Espacio = "0.900"
            // objReq.PPMRAC = "10"
            // objReq.NivelDesempeno = "1" 'podría ser entre 100
            // objReq.PorcDesperdicio = "0.05" ' entre 100
            // objReq.ENAlimento = "2500"
            // objReq.EMEquivalente = "3352"

            // objReq.IniciadorPesoInicial = 15
            // objReq.IniciadorPesoFinal = 30
            // objReq.CrecimientoPesoFinal = 50
            // objReq.DesarrolloPesoFinal = 50
            // objReq.FinalizadorPesoFinal = 90
            // objReq.FinRactopaminaPesoFinal = 120
            if (objReq == null) throw new Exception("Dato incorrecto en la entrada");
            ValidatePorcGdp(objReq);

            string strSQLRef = "SELECT * FROM CatOptimizerC_Referencias ";
            dtRef = Database.execQuery(strSQLRef);

            string strSQLVar = "SELECT * FROM CatOptimizerC_Variables ";
            dtVar = Database.execQuery(strSQLVar);

            string strSQLConst = "SELECT * FROM CatOptimizerC_Constantes ";
            dtConst = Database.execQuery(strSQLConst);
            

            string strSQLParam = "SELECT * FROM CatOptimizerC_ParametrosEconomicos ";
            dtParam = Database.execQuery(strSQLParam);

            // clasificaremos por variables identificador-variable para con ello ver dependencias en el llenado de la información
            // también veremos si hay variables calculadas  dependientes de otras
            // ocuparemos un request de petición para saber que datos serán inyectados 
            // tendremos tablas con la información que será convertida de los datos de entrada

            ResponseModel objResp = new ResponseModel();

            objResp.Variables.Add(GetVariable1(objReq));
            objResp.Variables.Add(GetVariable2(objReq));
            objResp.Variables.Add(GetVariable3(objResp, objReq));
            objResp.Variables.Add(GetVariable4(objResp, objReq));
            objResp.Variables.Add(GetVariable5(objResp, objReq));
            objResp.Variables.Add(GetVariable8(objResp, objReq));
            objResp.Variables.Add(GetVariable6(objResp, objReq));
            objResp.Variables.Add(GetVariable7(objResp, objReq));
            objResp.Variables.Add(GetVariable9(objResp, objReq));
            objResp.Variables.Add(GetVariable10(objResp, objReq));
            objResp.Variables.Add(GetVariable11(objResp, objReq));
            objResp.Variables.Add(GetVariable12(objResp, objReq));
            objResp.Variables.Add(GetVariable13(objResp, objReq));
            objResp.Variables.Add(GetVariable14(objResp, objReq));
            objResp.Variables.Add(GetVariable15(objResp, objReq));
            objResp.Variables.Add(GetVariable16(objResp, objReq));
            objResp.Variables.Add(GetVariable17(objResp, objReq));
            objResp.Variables.Add(GetVariable18(objResp, objReq));
            objResp.Variables.Add(GetVariable19(objResp, objReq));
            objResp.Variables.Add(GetVariable20(objResp, objReq));
            objResp.Variables.Add(GetVariable21(objResp, objReq));
            objResp.Variables.Add(GetVariable22(objResp, objReq));
            objResp.Variables.Add(GetVariable23(objResp, objReq));
            objResp.Variables.Add(GetVariable24(objResp, objReq));
            objResp.Variables.Add(GetVariable25(objResp, objReq));
            objResp.Variables.Add(GetVariable26(objResp, objReq));
            objResp.Variables.Add(GetVariable27(objResp, objReq));
            objResp.Variables.Add(GetVariable28(objResp, objReq));
            objResp.Variables.Add(GetVariable29(objResp, objReq));
            objResp.Variables.Add(GetVariable30(objResp, objReq));
            objResp.Variables.Add(GetVariable31(objResp, objReq));
            objResp.Variables.Add(GetVariable32(objResp, objReq));
            objResp.Variables.Add(GetVariable33(objResp, objReq));
            objResp.Variables.Add(GetVariable34(objResp, objReq));
            objResp.Variables.Add(GetVariable35(objResp, objReq));
            objResp.Variables.Add(GetVariable36(objResp, objReq));
            objResp.Variables.Add(GetVariable37(objResp, objReq));
            objResp.Variables.Add(GetVariable38(objResp, objReq));
            objResp.Variables.Add(GetVariable39(objResp, objReq));
            objResp.Variables.Add(GetVariable40(objResp, objReq));
            objResp.Variables.Add(GetVariable41(objResp, objReq));
            objResp.Variables.Add(GetVariable42(objResp, objReq));
            objResp.Variables.Add(GetVariable43(objResp, objReq));
            objResp.Variables.Add(GetVariable44(objResp, objReq));
            objResp.Variables.Add(GetVariable45(objResp, objReq));
            objResp.Variables.Add(GetVariable46(objResp, objReq));
            objResp.Variables.Add(GetVariable47(objResp, objReq));
            objResp.Variables.Add(GetVariable48(objResp, objReq));

            if (objReq.CvePerfilN > 0)
                SaveData(objResp, objReq);
            
            //string jsonString = JsonSerializer.Serialize<ResponseModel>(objResp);
            //string jsonString= JsonConvert.SerializeObject(objResp);
            //Console.WriteLine(jsonString);

            return objResp;
        }

            private  bool IsNumeric(string text)
            {
                double test;
                return double.TryParse(text, out test);
            }

            private void SaveData(ResponseModel objResp, RequestModel objReq)
        {
            string strSQLParam = "DELETE OptimizerC_PerfilN_Resultado WHERE CvePerfilN =" + objReq.CvePerfilN.ToString() + "";
            Database.execNonQuery(strSQLParam);

            strSQLParam = "INSERT INTO OptimizerC_PerfilN_Resultado(CvePerfilN,Request,Response,Response2,FecAct,UsuAct) ";
            string jsonResp = JsonConvert.SerializeObject(objResp);
            string jsonReq = JsonConvert.SerializeObject(objReq);
            strSQLParam += "VALUES(" + objReq.CvePerfilN.ToString() + ",'" + jsonReq + "','" + jsonResp + "','" + jsonResp + "',GETDATE(),'" + objReq.UsuAct + "') ";
            Database.execNonQuery(strSQLParam);
        }


        private ResponseDataModel GetVariable1(RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            variable.NoVariable = 1;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p => new EtapaResModel(p.Clave, p.PesoInicial)).ToList();
            return variable;
        }
        private ResponseDataModel GetVariable2(RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            variable.NoVariable = 2;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p => new EtapaResModel(p.Clave, p.PesoFinal)).ToList();
            return variable;
        }

        private ResponseDataModel GetVariable3(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);

            variable.NoVariable = 3;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                return new EtapaResModel(p.Clave, valor2 - valor1);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable4(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);

            variable.NoVariable = 4;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");

            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double gdpReferencia = 0;
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);

                if (objReq.Referencia == 5 | objReq.Referencia == 6)
                {
                    int ref1 = 1;
                    int ref2 = 3;
                    if (objReq.Referencia == 6)
                    {
                        ref1 = 2;
                        ref2 = 4;
                    }

                    double varA1 = GetReferencias(ref1, "valorA");
                    double varB1 = GetReferencias(ref1, "valorB");
                    double varC1 = GetReferencias(ref1, "valorC");

                    double varA2 = GetReferencias(ref2, "valorA");
                    double varB2 = GetReferencias(ref2, "valorB");
                    double varC2 = GetReferencias(ref2, "valorC");

                    valor1 = varA1 * Math.Pow(promedio, 2) + varB1 * promedio + varC1;
                    valor2 = varA2 * Math.Pow(promedio, 2) + varB2 * promedio + varC2;

                    gdpReferencia = GetPromedio(valor1, valor2);
                }
                else
                {

                    // =$V$7*PROMEDIO(F5F6)^2+$V$8*PROMEDIO(F5:F6)+$V$9
                    double varA = GetReferencias(objReq.Referencia, "valorA");
                    double varB = GetReferencias(objReq.Referencia, "valorB");
                    double varC = GetReferencias(objReq.Referencia, "valorC");

                    gdpReferencia = varA * Math.Pow(promedio, 2) + varB * promedio + varC;
                }
                if (p.IsRactopamina)
                {
                    // +((-0.1*C7^2 + 4.5*C7 + 80)/1000)
                    // valor += ((-0.1 * Math.Pow(objReq.PPMRAC, 2) + 4.5 * objReq.PPMRAC + 80) / 1000)
                    double c1 = GetConstantes(objReq.Referencia, 4, 1);
                    double c2 = GetConstantes(objReq.Referencia, 4, 2);
                    double c3 = GetConstantes(objReq.Referencia, 4, 3);
                    double c4 = GetConstantes(objReq.Referencia, 4, 4);

                    gdpReferencia += ((c1 * Math.Pow(objReq.PPMRAC, 2) + c2 * objReq.PPMRAC + c3) / c4);
                }
                double gdpAjustado = GetGdpAjustado(gdpReferencia, p.PorcGDP);
                return new EtapaResModel(p.Clave, gdpAjustado)
                {
                    ValorReferencia = gdpReferencia
                };
            }).ToList();

            return variable;
        }

        private static double GetGdpAjustado(double gdpReferencia, double? porcGdp)
        {
            if (!porcGdp.HasValue || porcGdp.Value == 0)
                return gdpReferencia;

            return gdpReferencia * (1 + (porcGdp.Value / 100));
        }

        private static void ValidatePorcGdp(RequestModel objReq)
        {
            foreach (EtapaModel etapa in objReq.EtapasModel.Where(e => e.PorcGDP.HasValue && e.PorcGDP.Value != 0))
            {
                if (etapa.PorcGDP.Value < -5 || etapa.PorcGDP.Value > 10)
                {
                    throw new Exception($"El porcentaje de GDP de la etapa {etapa.Clave} debe estar entre -5 y 10.");
                }
            }
        }

        private ResponseDataModel GetVariable5(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 3);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 4);

            variable.NoVariable = 5;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                return new EtapaResModel(p.Clave, valor1 / valor2);
            }).ToList();

            return variable;
        }


        private ResponseDataModel GetVariable6(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable4 = Utileria.GetVariableByNum(objResp, 4);

            variable.NoVariable = 6;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor = 0;
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor4 = variable4.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                // Dim varA As Double = 106
                // Dim varB As Double = 2182.6
                // Dim varC As Double = 70.886
                // Dim varD As Double = 0.2026
                // If objReq.Referencia = 3 Or objReq.Referencia = 4 Then
                // varA = 106
                // varB = 2211
                // varC = 76.66
                // varD = 0.3726
                // End If
                // '2117.5+55.873*PROMEDIO(F5:F6)-0.1664
                // If objReq.Referencia = 7 Then
                // varA = 106
                // varB = 2117.5
                // varC = 55.873
                // varD = 0.1664
                // End If

                double varA = GetConstantes(objReq.Referencia, 6, 1);
                double varB = GetConstantes(objReq.Referencia, 6, 2);
                double varC = GetConstantes(objReq.Referencia, 6, 3);
                double varD = GetConstantes(objReq.Referencia, 6, 4);

                double factorAdi = 1;
                if (objReq.Referencia == 5 | objReq.Referencia == 6)
                {
                    double varA1 = GetConstantes(1, 6, 1);
                    double varB1 = GetConstantes(1, 6, 2);
                    double varC1 = GetConstantes(1, 6, 3);
                    double varD1 = GetConstantes(1, 6, 4);

                    double varA2 = GetConstantes(3, 6, 1);
                    double varB2 = GetConstantes(3, 6, 2);
                    double varC2 = GetConstantes(3, 6, 3);
                    double varD2 = GetConstantes(3, 6, 4);

                    if (objReq.Referencia == 6)
                        factorAdi = 0.95;
                    valor1 = (varA1 * Math.Pow(promedio, 0.75) + (varB1 + varC1 * promedio - varD1 * Math.Pow(promedio, 2)) * valor4);
                    valor2 = (varA2 * Math.Pow(promedio, 0.75) + (varB2 + varC2 * promedio - varD2 * Math.Pow(promedio, 2)) * valor4);
                    valor = GetPromedio(valor1, valor2) * factorAdi;
                }
                else
                {

                    // =(106*PROMEDIO(F5:F6)^0.75)+(2182.6+70.886*PROMEDIO(F5:F6)-0.2026*PROMEDIO(F5:F6)^2)*F8

                    if (objReq.Referencia == 2 | objReq.Referencia == 4)
                        factorAdi = GetConstantes(objReq.Referencia, 6, 5);// 0.95

                    valor = (varA * Math.Pow(promedio, 0.75) + (varB + varC * promedio - varD * Math.Pow(promedio, 2)) * valor4) * factorAdi;
                }
                // If p.IsRactopamina Then
                // '+((-0.1*C7^2 + 4.5*C7 + 80)/1000)
                // valor += ((-0.1 * Math.Pow(objReq.PPMRAC, 2) + 4.5 * objReq.PPMRAC + 80) / 1000)
                // End If

                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable7(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable8 = Utileria.GetVariableByNum(objResp, 8);

            variable.NoVariable = 7;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor = variable8.Etapas.Find(e => e.Clave == p.Clave).Valor;
                // Return New EtapaResModel(p.Clave, valor / 0.75)
                return new EtapaResModel(p.Clave, valor / GetConstantes(objReq.Referencia, 7, 1));
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable8(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();

            variable.NoVariable = 8;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor = objReq.EtapasModel.Find(e => e.Clave == p.Clave).ENAlimento;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }
        private ResponseDataModel GetVariable9(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable6 = Utileria.GetVariableByNum(objResp, 6);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);

            variable.NoVariable = 9;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor6 = variable6.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor7 = variable7.Etapas.Find(e => e.Clave == p.Clave).Valor;
                return new EtapaResModel(p.Clave, valor6 / valor7);
            }).ToList();

            return variable;
        }

        // =0.000173793452518262*PROMEDIO(F5:F6)^2 -0.0669444765389833*PROMEDIO(F5:F6) + 26.4087617382871
        private ResponseDataModel GetVariable10(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);

            variable.NoVariable = 10;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 10, 1);
                double c2 = GetConstantes(objReq.Referencia, 10, 2);
                double c3 = GetConstantes(objReq.Referencia, 10, 3);
                // Dim valor As Double = 0.000173793452518262 * Math.Pow(promedio, 2) - 0.0669444765389833 * promedio + 26.4087617382871
                double valor = c1 * Math.Pow(promedio, 2) - c2 * promedio + c3;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable11(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable10 = Utileria.GetVariableByNum(objResp, 10);

            variable.NoVariable = 11;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            // =2.4*(PROMEDIO(F5:F6)^0.75)*(F16-$C$5)
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor10 = variable10.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 11, 1);
                // Dim valor As Double = 2.4 * (Math.Pow(promedio, 0.75)) * (valor10 - objReq.Temperatura)
                double valor = c1 * (Math.Pow(promedio, 0.75)) * (valor10 - objReq.Temperatura);
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }
        private ResponseDataModel GetVariable12(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable6 = Utileria.GetVariableByNum(objResp, 6);
            ResponseDataModel variable11 = Utileria.GetVariableByNum(objResp, 11);


            variable.NoVariable = 12;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            // =F11+F17
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor6 = variable6.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor11 = variable11.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor6 + valor11;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable13(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable12 = Utileria.GetVariableByNum(objResp, 12);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);


            variable.NoVariable = 13;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            // =2.4*(PROMEDIO(F5:F6)^0.75)*(F16-$C$5)
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor12 = variable12.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor7 = variable7.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor12 / valor7;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable14(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 14;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =0.0336*PROMEDIO(F5:F6)^0.667
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 14, 1);
                double c2 = GetConstantes(objReq.Referencia, 14, 2);
                // Dim valor As Double = 0.0336 * Math.Pow(promedio, 0.667)
                double valor = c1 * Math.Pow(promedio, c2);
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable15(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable14 = Utileria.GetVariableByNum(objResp, 14);

            variable.NoVariable = 15;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =$C$6/F21
                double valor14 = variable14.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = objReq.Espacio / valor14;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }
        private ResponseDataModel GetVariable16(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable15 = Utileria.GetVariableByNum(objResp, 15);
            ResponseDataModel variable12 = Utileria.GetVariableByNum(objResp, 12);


            variable.NoVariable = 16;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =SI(F22>1,F18,(F18-((0.252*(1-F22)*F18))))
                double valor;
                double valor15 = variable15.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor12 = variable12.Etapas.Find(e => e.Clave == p.Clave).Valor;
                if (valor15 > 1)
                    valor = valor12;
                else
                {
                    double c1 = GetConstantes(objReq.Referencia, 16, 1);
                    // valor = (valor12 - ((0.252 * (1 - valor15) * valor12)))
                    valor = (valor12 - ((c1 * (1 - valor15) * valor12)));
                }

                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }
        private ResponseDataModel GetVariable17(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable16 = Utileria.GetVariableByNum(objResp, 16);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);


            variable.NoVariable = 17;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor16 = variable16.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor7 = variable7.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor16 / valor7;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable18(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable3 = Utileria.GetVariableByNum(objResp, 3);
            ResponseDataModel variable16 = Utileria.GetVariableByNum(objResp, 16);

            variable.NoVariable = 18;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.FindAll(p => p.IsRactopamina).Select(p =>
            {
                // =(1-((-0.191263+(0.019013*$K$7)-(0.000443*$K$7^2)+(0.000003539*$K$7^3))*($C$7/20)^0.7))*$K$23
                double valor3 = variable3.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor16 = variable16.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double c1 = GetConstantes(objReq.Referencia, 18, 1);
                double c2 = GetConstantes(objReq.Referencia, 18, 2);
                double c3 = GetConstantes(objReq.Referencia, 18, 3);
                double c4 = GetConstantes(objReq.Referencia, 18, 4);
                double c5 = GetConstantes(objReq.Referencia, 18, 5);
                double c6 = GetConstantes(objReq.Referencia, 18, 6);
                // Dim valor As Double = (1 - ((-0.191263 + (0.019013 * valor3) - (0.000443 * Math.Pow(valor3, 2)) + (0.000003539 * Math.Pow(valor3, 3))) * (objReq.PPMRAC / 20) ^ 0.7)) * valor16
                double valor = (1 - ((c1 + (c2 * valor3) - (c3 * Math.Pow(valor3, 2)) + (c4 * Math.Pow(valor3, 3))) * Math.Pow((objReq.PPMRAC / c5), c6))) * valor16;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable19(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable18 = Utileria.GetVariableByNum(objResp, 18);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);

            variable.NoVariable = 19;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.FindAll(p => p.IsRactopamina).Select(p =>
            {
                // =K26/K12
                double valor18 = variable18.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor7 = variable7.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor18 / valor7;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }


        private ResponseDataModel GetVariable20(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable10 = Utileria.GetVariableByNum(objResp, 10);

            variable.NoVariable = 20;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =(111*PROMEDIO(F5:F6)^0.803+111*PROMEDIO(F5:F6)^0.803*(F16-$C$5)*0.025)/1000
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double valor10 = variable10.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double c1 = GetConstantes(objReq.Referencia, 20, 1);
                double c2 = GetConstantes(objReq.Referencia, 20, 2);
                double c3 = GetConstantes(objReq.Referencia, 20, 3);
                double c4 = GetConstantes(objReq.Referencia, 20, 4);
                double c5 = GetConstantes(objReq.Referencia, 20, 5);
                double c6 = GetConstantes(objReq.Referencia, 20, 6);
                // Dim valor As Double = (111 * Math.Pow(promedio, 0.803) + 111 * Math.Pow(promedio, 0.803) * (valor10 - objReq.Temperatura) * 0.025) / 1000
                double valor = (c1 * Math.Pow(promedio, c2) + c3 * Math.Pow(promedio, c4) * (valor10 - objReq.Temperatura) * c5) / c6;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable21(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable17 = Utileria.GetVariableByNum(objResp, 17);
            ResponseDataModel variable5 = Utileria.GetVariableByNum(objResp, 5);
            ResponseDataModel variable19 = Utileria.GetVariableByNum(objResp, 19);

            variable.NoVariable = 21;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =F9*F24
                double valor17 = variable17.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor5 = variable5.Etapas.Find(e => e.Clave == p.Clave).Valor;
                if (p.IsRactopamina)
                    valor17 = variable19.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor17 * valor5;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable22(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable21 = Utileria.GetVariableByNum(objResp, 21);
            ResponseDataModel variable3 = Utileria.GetVariableByNum(objResp, 3);


            variable.NoVariable = 22;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =F32/F7
                double valor21 = variable21.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor3 = variable3.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor21 / valor3;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable23(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            // comparo variable 2 y variable1        
            ResponseDataModel variable21 = Utileria.GetVariableByNum(objResp, 21);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable10 = Utileria.GetVariableByNum(objResp, 10);
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);

            variable.NoVariable = 23;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            int etapaMin = objReq.EtapasModel.Min(e => e.Clave);
            int etapaMax=objReq.EtapasModel.Max(e => e.Clave);
            variable.Etapas = objReq.EtapasModel.FindAll(p => p.Clave == etapaMax | p.IsRactopamina).Select(p =>
            {
                if (variable1.Etapas.Find(e => e.Clave == etapaMin) == null)
                    return new EtapaResModel(p.Clave, 0);
                double valor1 = variable1.Etapas.Find(e => e.Clave == etapaMin).Valor;
                double valor;
                if (p.IsRactopamina == false)
                {
                    // =SUMA(F32:I32,K32)/(K6-F5)
                    double suma = variable21.Etapas.FindAll(r => r.Clave != etapaMax).Sum(r => r.Valor);
                    double valor2 = variable2.Etapas.Find(e => e.Clave == etapaMax ).Valor;
                    valor = suma / (valor2 - valor1);
                }
                else
                {
                    // =SUMA(F32:J32)/(J6-F5)
                    double suma = variable21.Etapas.FindAll(r => r.Clave != etapaMax ).Sum(r => r.Valor);
                    double valor2 = variable2.Etapas.Find(e => e.Clave == etapaMax ).Valor;
                    valor = suma / (valor2 - valor1);
                }

                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }


        private ResponseDataModel GetVariable24(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable4 = Utileria.GetVariableByNum(objResp, 4);

            variable.NoVariable = 24;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");

            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =(0.036*PROMEDIO(F5:F6)^0.75)+(16.4525+0.09005*PROMEDIO(F5:F6)-0.0004*PROMEDIO(F5:F6)^2)*F8
                // =(0.036*PROMEDIO(F5:F6)^0.75)+(16.664+0.0736*PROMEDIO(F5:F6)-0.0003*PROMEDIO(F5:F6)^2)*F8
                // =(0.036*PROMEDIO(F5:F6)^0.75)+(16.664+0.0736*PROMEDIO(F5:F6)-0.0003*PROMEDIO(F5:F6)^2)*F8
                // 7=(0.036*PROMEDIO(F5:F6)^0.75)+(16.241+0.1065*PROMEDIO(F5:F6)-0.0005*PROMEDIO(F5:F6)^2)*F8
                // 8=(0.036*PROMEDIO(F5:F6)^0.75)+(16.241+0.1065*PROMEDIO(F5:F6)-0.0005*PROMEDIO(F5:F6)^2)*F8
                // 9=(0.036*PROMEDIO(F5:F6)^0.75)+(16.4525+0.09005*PROMEDIO(F5:F6)-0.0004*PROMEDIO(F5:F6)^2)*F8
                // 10=(0.036*PROMEDIO(F5:F6)^0.75)+(16.4525+0.09005*PROMEDIO(F5:F6)-0.0004*PROMEDIO(F5:F6)^2)*F8
                // 11=(0.036*PROMEDIO(F5:F6)^0.75) + (15.795 + 0.142*PROMEDIO(F5:F6)-0.0008*PROMEDIO(F5:F6)^2)*F8
                // Dim varA As Double = 0.036
                // Dim varB As Double = 16.664
                // Dim varC As Double = 0.0736
                // Dim varD As Double = 0.0003

                double varA = GetConstantes(objReq.Referencia, 24, 1);
                double varB = GetConstantes(objReq.Referencia, 24, 2);
                double varC = GetConstantes(objReq.Referencia, 24, 3);
                double varD = GetConstantes(objReq.Referencia, 24, 4);

                double valor = 0;
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor4 = variable4.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);

                // If objReq.Referencia = 3 Or objReq.Referencia = 4 Then
                // varA = 0.036
                // varB = 16.241
                // varC = 0.1065
                // varD = 0.0005
                // End If
                // If objReq.Referencia = 5 Or objReq.Referencia = 6 Then
                // varA = 0.036
                // varB = 16.4525
                // varC = 0.09005
                // varD = 0.0004
                // End If
                // If objReq.Referencia = 7 Then
                // varA = 0.036
                // varB = 15.795
                // varC = 0.142
                // varD = 0.0008
                // End If
                valor = (varA * Math.Pow(promedio, 0.75)) + (varB + varC * promedio - varD * Math.Pow(promedio, 2)) * valor4;
                if (p.IsRactopamina)
                {
                    double var5 = GetConstantes(objReq.Referencia, 24, 5);
                    double var6 = GetConstantes(objReq.Referencia, 24, 6);
                    double var7 = GetConstantes(objReq.Referencia, 24, 7);
                    // valor += (-0.003 * Math.Pow(objReq.PPMRAC, 2) + 0.173 * objReq.PPMRAC + 3.125)
                    valor += (var5 * Math.Pow(objReq.PPMRAC, 2) + var6 * objReq.PPMRAC + var7);
                }

                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable25(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable24 = Utileria.GetVariableByNum(objResp, 24);
            ResponseDataModel variable4 = Utileria.GetVariableByNum(objResp, 4);


            variable.NoVariable = 25;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor24 = variable24.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor4 = variable4.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor24 / valor4;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable26(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable24 = Utileria.GetVariableByNum(objResp, 24);
            ResponseDataModel variable17 = Utileria.GetVariableByNum(objResp, 17);
            ResponseDataModel variable19 = Utileria.GetVariableByNum(objResp, 19);


            variable.NoVariable = 26;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor24 = variable24.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor17 = variable17.Etapas.Find(e => e.Clave == p.Clave).Valor;
                if (p.IsRactopamina)
                    valor17 = variable19.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = (valor24 / valor17) / 10;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }
        private ResponseDataModel GetVariable27(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable26 = Utileria.GetVariableByNum(objResp, 26);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);


            variable.NoVariable = 27;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor26 = variable26.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor7 = variable7.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor26 / valor7 * 10000;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable28(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable26 = Utileria.GetVariableByNum(objResp, 26);
            ResponseDataModel variable8 = Utileria.GetVariableByNum(objResp, 8);


            variable.NoVariable = 28;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                double valor26 = variable26.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor8 = variable8.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor = valor26 / valor8 * 10000;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable29(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 29;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = 0.0002*PROMEDIO(F5:F6) + 0.2974
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 29, 1);
                double c2 = GetConstantes(objReq.Referencia, 29, 2);
                // Dim valor As Double = 0.0002 * promedio + 0.2974
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable30(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 30;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = 0.0004*PROMEDIO(F5:F6) + 0.596
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 30, 1);
                double c2 = GetConstantes(objReq.Referencia, 30, 2);
                // Dim valor As Double = 0.0004 * promedio + 0.596
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable31(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 31;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // 0.0005*PROMEDIO(F5:F6) + 0.6385
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 31, 1);
                double c2 = GetConstantes(objReq.Referencia, 31, 2);
                // Dim valor As Double = 0.0005 * promedio + 0.6385
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable32(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 32;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = -0.0001*PROMEDIO(F5:F6) + 0.1932
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 32, 1);
                double c2 = GetConstantes(objReq.Referencia, 32, 2);
                // Dim valor As Double = -0.0001 * promedio + 0.1932
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }
        private ResponseDataModel GetVariable33(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 33;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = -0.0001*PROMEDIO(F5:F6) + 0.1932
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 33, 1);
                double c2 = GetConstantes(objReq.Referencia, 33, 2);
                // Dim valor As Double = -0.00002 * promedio + 0.6738
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable34(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 34;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // 0.0001*PROMEDIO(F5:F6) + 0.5183
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 34, 1);
                double c2 = GetConstantes(objReq.Referencia, 34, 2);
                // Dim valor As Double = 0.0001 * promedio + 0.5183
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }
        private ResponseDataModel GetVariable35(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 35;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = -0.00008*PROMEDIO(F5:F6) + 0.4147
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 35, 1);
                double c2 = GetConstantes(objReq.Referencia, 35, 2);
                // Dim valor As Double = -0.00008 * promedio + 0.4147
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable36(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 36;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = -0.0001*PROMEDIO(F5:F6) + 1.0043
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 36, 1);
                double c2 = GetConstantes(objReq.Referencia, 36, 2);
                // Dim valor As Double = -0.0001 * promedio + 1.0043
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable37(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 37;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = 0.00002*PROMEDIO(F5:F6) + 0.3186
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 37, 1);
                double c2 = GetConstantes(objReq.Referencia, 37, 2);
                // Dim valor As Double = 0.00002 * promedio + 0.3186
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable38(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);


            variable.NoVariable = 38;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = 0.0002*PROMEDIO(F5:F6) + 0.9337
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 38, 1);
                double c2 = GetConstantes(objReq.Referencia, 38, 2);
                // Dim valor As Double = 0.0002 * promedio + 0.9337
                double valor = c1 * promedio + c2;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable39(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable24 = Utileria.GetVariableByNum(objResp, 24);


            variable.NoVariable = 39;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = 1.8453*F36^1.0599
                double valor24 = variable24.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double c1 = GetConstantes(objReq.Referencia, 39, 1);
                double c2 = GetConstantes(objReq.Referencia, 39, 2);
                // Dim valor As Double = 1.8453 * Math.Pow(valor24, 1.0599)
                double valor = c1 * Math.Pow(valor24, c2);
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable40(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);

            variable.NoVariable = 40;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // = ((224.7-1.926*PROMEDIO(F5:F6)+0.0092*PROMEDIO(F5:F6)^2)/ 1000)*(F12/1000)
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor7 = variable7.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 40, 1);
                double c2 = GetConstantes(objReq.Referencia, 40, 2);
                double c3 = GetConstantes(objReq.Referencia, 40, 3);
                // Dim valor As Double = ((224.7 - 1.926 * promedio + 0.0092 * Math.Pow(promedio, 2)) / 1000) * (valor7 / 1000)
                double valor = ((c1 - c2 * promedio + c3 * Math.Pow(promedio, 2)) / 1000) * (valor7 / 1000);
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable41(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable4 = Utileria.GetVariableByNum(objResp, 4);
            ResponseDataModel variable17 = Utileria.GetVariableByNum(objResp, 17);
            ResponseDataModel variable19 = Utileria.GetVariableByNum(objResp, 19);

            variable.NoVariable = 41;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =(_xlfn.IFS(F6<="50",0.046*PROMEDIO(F5:F6)^0.75+5.81*F8,F6>"50",0.046*PROMEDIO(F5:F6)^0.75+5.33*F8))/(F24*10)
                double valor = 0;
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor4 = variable4.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor17 = variable17.Etapas.Find(e => e.Clave == p.Clave).Valor;

                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 41, 1);
                double c2 = GetConstantes(objReq.Referencia, 41, 2);
                double c3 = GetConstantes(objReq.Referencia, 41, 3);
                double c4 = GetConstantes(objReq.Referencia, 41, 4);
                double c5 = GetConstantes(objReq.Referencia, 41, 5);
                double c6 = GetConstantes(objReq.Referencia, 41, 6);
                double c7 = GetConstantes(objReq.Referencia, 41, 7);
                double c8 = GetConstantes(objReq.Referencia, 41, 8);
                // If valor2 <= 50 Then
                // '0.046*PROMEDIO(F5:F6)^0.75+5.81*F8
                // valor = 0.046 * Math.Pow(promedio, 0.75) + 5.81 * valor4
                // Else
                // '0.046*PROMEDIO(F5:F6)^0.75+5.33*F8))/(F24*10)
                // valor = 0.046 * Math.Pow(promedio, 0.75) + 5.33 * valor4
                // End If
                if (valor2 <= c1)
                    // 0.046*PROMEDIO(F5:F6)^0.75+5.81*F8
                    valor = c2 * Math.Pow(promedio, c3) + c4 * valor4;
                else
                    // 0.046*PROMEDIO(F5:F6)^0.75+5.33*F8))/(F24*10)
                    valor = c5 * Math.Pow(promedio, c6) + c7 * valor4;

                // valor /= (valor17 * c8)
                if (p.IsRactopamina)
                {
                    double valor19 = variable19.Etapas.Find(e => e.Clave == p.Clave).Valor;
                    // SaveSetting=/(K27*10)
                    valor /= (valor19 * c8);
                }
                else
                    valor /= (valor17 * c8);
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable42(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable4 = Utileria.GetVariableByNum(objResp, 4);
            ResponseDataModel variable17 = Utileria.GetVariableByNum(objResp, 17);
            ResponseDataModel variable19 = Utileria.GetVariableByNum(objResp, 19);

            variable.NoVariable = 42;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =(_xlfn.IFS(F6<="50",0.046*PROMEDIO(F5:F6)^0.75+5.6*F8,F6>"50",0.046*PROMEDIO(F5:F6)^0.75+5.3*F8))/(F24*10)
                double valor = 0;
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor4 = variable4.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor17 = variable17.Etapas.Find(e => e.Clave == p.Clave).Valor;

                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 42, 1);
                double c2 = GetConstantes(objReq.Referencia, 42, 2);
                double c3 = GetConstantes(objReq.Referencia, 42, 3);
                double c4 = GetConstantes(objReq.Referencia, 42, 4);
                double c5 = GetConstantes(objReq.Referencia, 42, 5);
                double c6 = GetConstantes(objReq.Referencia, 42, 6);
                double c7 = GetConstantes(objReq.Referencia, 42, 7);
                double c8 = GetConstantes(objReq.Referencia, 42, 8);
                // If valor2 <= 50 Then
                // '0.046*PROMEDIO(F5:F6)^0.75+5.6*F8
                // valor = 0.046 * Math.Pow(promedio, 0.75) + 5.6 * valor4
                // Else
                // '0.046*PROMEDIO(F5:F6)^0.75+5.3*F8
                // valor = 0.046 * Math.Pow(promedio, 0.75) + 5.3 * valor4
                // End If
                if (valor2 <= c1)
                    valor = c2 * Math.Pow(promedio, c3) + c4 * valor4;
                else
                    valor = c5 * Math.Pow(promedio, c6) + c7 * valor4;

                // valor /= (valor17 * 10)
                if (p.IsRactopamina)
                {
                    double valor19 = variable19.Etapas.Find(e => e.Clave == p.Clave).Valor;
                    valor /= (valor19 * c8);
                }
                else
                    valor /= (valor17 * c8);

                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable43(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable42 = Utileria.GetVariableByNum(objResp, 42);
            ResponseDataModel variable8 = Utileria.GetVariableByNum(objResp, 8);

            variable.NoVariable = 43;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =F54/F13*10000
                double valor = 0;
                double valor42 = variable42.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor8 = variable8.Etapas.Find(e => e.Clave == p.Clave).Valor;

                valor = valor42 / valor8 * 10000;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable44(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();

            variable.NoVariable = 44;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // Dim valor As Double = 2.08 DEFAULT
                double valor = GetConstantes(objReq.Referencia, 44, 0);
                if (p.Clave > 1 & p.Clave < 7)
                    valor = GetConstantes(objReq.Referencia, 44, p.Clave);

                return new EtapaResModel(p.Clave, valor);
            }).ToList();
            return variable;
        }

        private ResponseDataModel GetVariable45(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable44 = Utileria.GetVariableByNum(objResp, 44);
            ResponseDataModel variable42 = Utileria.GetVariableByNum(objResp, 42);

            variable.NoVariable = 45;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =F56*F54
                double valor = 0;
                double valor42 = variable42.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor44 = variable44.Etapas.Find(e => e.Clave == p.Clave).Valor;

                valor = valor44 * valor42;
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable46(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);

            variable.NoVariable = 46;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =((154.9-0.427*PROMEDIO(F5:F6)+0.0006*PROMEDIO(F5:F6)^2)/1000)*(F12/1000)
                double valor = 0;
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor7 = variable7.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 46, 1);
                double c2 = GetConstantes(objReq.Referencia, 46, 2);
                double c3 = GetConstantes(objReq.Referencia, 46, 3);
                // valor = ((154.9 - 0.427 * promedio + 0.0006 * Math.Pow(promedio, 2)) / 1000) * (valor7 / 1000)
                valor = ((c1 - c2 * promedio + c3 * Math.Pow(promedio, 2)) / 1000) * (valor7 / 1000);
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable47(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);

            variable.NoVariable = 47;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =(((68.4-0.346*PROMEDIO(F5:F6)+0.0014*PROMEDIO(F5:F6)^2)/1000)*(F12/1000))+0.05
                double valor = 0;
                // Dim valor1 As Double = variable1.Etapas.Find(Function(e) e.Clave = p.Clave).Valor
                // Dim valor2 As Double = variable2.Etapas.Find(Function(e) e.Clave = p.Clave).Valor
                // Dim valor7 As Double = variable7.Etapas.Find(Function(e) e.Clave = p.Clave).Valor
                // Dim promedio As Double = GetPromedio(valor1, valor2)
                double c1 = GetConstantes(objReq.Referencia, 47, 1);
                // Dim c2 As Double = GetConstantes(objReq.Referencia, 47, 2)
                // Dim c3 As Double = GetConstantes(objReq.Referencia, 47, 3)
                // Dim c4 As Double = GetConstantes(objReq.Referencia, 47, 4)
                // valor = (((68.4 - 0.346 * promedio + 0.0014 * Math.Pow(promedio, 2)) / 1000) * (valor7 / 1000)) + 0.05
                valor = c1; // (((c1 - c2 * promedio + c3 * Math.Pow(promedio, 2)) / 1000) * (valor7 / 1000)) + c4
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        private ResponseDataModel GetVariable48(ResponseModel objResp, RequestModel objReq)
        {
            ResponseDataModel variable = new ResponseDataModel();
            ResponseDataModel variable1 = Utileria.GetVariableByNum(objResp, 1);
            ResponseDataModel variable2 = Utileria.GetVariableByNum(objResp, 2);
            ResponseDataModel variable7 = Utileria.GetVariableByNum(objResp, 7);

            variable.NoVariable = 48;
            variable.Variable = GetVariable(variable.NoVariable);
            variable.Posicion = int.Parse(GetVariable(variable.NoVariable, "Posicion"));
            variable.MostrarCliente = GetVariable(variable.NoVariable, "MostrarCliente");
            variable.Etapas = objReq.EtapasModel.Select(p =>
            {
                // =((65.4-0.346*PROMEDIO(F5:F6)+0.0014*PROMEDIO(F5:F6)^2)/1000)*(F12/1000)
                double valor = 0;
                double valor1 = variable1.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor2 = variable2.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double valor7 = variable7.Etapas.Find(e => e.Clave == p.Clave).Valor;
                double promedio = GetPromedio(valor1, valor2);
                double c1 = GetConstantes(objReq.Referencia, 48, 1);
                double c2 = GetConstantes(objReq.Referencia, 48, 2);
                double c3 = GetConstantes(objReq.Referencia, 48, 3);
                double c4 = GetConstantes(objReq.Referencia, 48, 4);

                // valor = ((65.4 - 0.346 * promedio + 0.0014 * Math.Pow(promedio, 2)) / 1000) * (valor7 / 1000)
                valor = ((c1 - c2 * promedio + c3 * Math.Pow(promedio, 2)) / 1000) * (valor7 / 1000);
                return new EtapaResModel(p.Clave, valor);
            }).ToList();

            return variable;
        }

        
        
     

        public string GetVariables(int cveVariable, string cveColumna)
        {
            foreach (DataRow dtR in dtVar.Rows)
            {
                if (dtR["CveVariable"].Equals(cveVariable))
                    return dtR[cveColumna].ToString();
            }
            return "";
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

        public double GetReferencias(int cveReferencia, string cveColumna)
        {
            if (dtRef != null)
            {
                foreach (DataRow dtR in dtRef.Rows)
                {
                    if (dtR["CveReferencia"].Equals(cveReferencia))
                    {
                        return double.Parse((string)dtR[cveColumna]);
                    }

                }
            }
            return 0;
        }

        private string GetVariable(int cveVariable, string columna = "NomVariable")
        {
            if (dtVar != null)
            {
                foreach (DataRow dtR in dtVar.Rows)
                {
                    if (dtR["CveVariable"].Equals(cveVariable))
                    {
                        string? v = dtR[columna].ToString();
                        return v;
                    }
                }
            }
            return "";
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
    }

}
