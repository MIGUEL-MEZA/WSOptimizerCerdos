namespace WSOptimizer7.Controllers
{
    using System;
    using WSOptimizer7.Models;

    public class Utileria
    {
        public static ResponseDataModel GetVariableByNum(ResponseModel objResp, int noEtapa)
        {
            try
            {
                ResponseDataModel objData = objResp.Variables.Find(p => p.NoVariable == noEtapa);
                return objData;
            }
            catch (Exception ex)
            {
                throw new Exception("No se encontró la etapa ");
            }
        }
    }

}
