<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")
flag = request.querystring("flag")
SelectedDate = request.querystring("SelectedDate")

if flag = "PO" then 

	sql = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, MS_PO.STATUS, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BILLOUTACC, BILLOUTFIN, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME, FIRST_RCVDATE, BILL_END_ACC, APPROVE_NO_FIN, CASE WHEN BILL_PAY_FIN IS NULL THEN DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) ELSE DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) END AS PROCESSDAY FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE Left([PO_NO],2)='" & right(Budget,2) & "' and MS_PO.STATUS not in ('0','C') ORDER BY MS_PO.REAL_PO;"

elseif flag = "RCV" then 
	
	sql = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, MS_PO.STATUS, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BILLOUTACC, BILLOUTFIN, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME, FIRST_RCVDATE, BILL_END_ACC, APPROVE_NO_FIN, CASE WHEN BILL_PAY_FIN IS NULL THEN DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) ELSE DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) END AS PROCESSDAY FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE Left([PO_NO],2)='" & right(Budget,2) & "' and MS_PO.STATUS in ('2','3','4','5','6','7','8','9','D') ORDER BY MS_PO.REAL_PO;"

elseif flag = "ACC" then 
	
	sql = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, MS_PO.STATUS, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BILLOUTACC, BILLOUTFIN, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME, FIRST_RCVDATE, BILL_END_ACC, APPROVE_NO_FIN, CASE WHEN BILL_PAY_FIN IS NULL THEN DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) ELSE DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) END AS PROCESSDAY FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE Left([PO_NO],2)='" & right(Budget,2) & "' and MS_PO.STATUS in ('4','5','6','7','8','9','D') ORDER BY MS_PO.REAL_PO;"

elseif flag = "FIN" then 
	
	sql = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, MS_PO.STATUS, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BILLOUTACC, BILLOUTFIN, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME, FIRST_RCVDATE, BILL_END_ACC, APPROVE_NO_FIN, CASE WHEN BILL_PAY_FIN IS NULL THEN DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) ELSE DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) END AS PROCESSDAY FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE Left([PO_NO],2)='" & right(Budget,2) & "' and MS_PO.STATUS in ('4','5','7','8','9') ORDER BY MS_PO.REAL_PO;"
	
elseif flag = "END" then 
	
	sql = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, MS_PO.STATUS, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BILLOUTACC, BILLOUTFIN, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME, FIRST_RCVDATE, BILL_END_ACC, APPROVE_NO_FIN, CASE WHEN BILL_PAY_FIN IS NULL THEN DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) ELSE DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) END AS PROCESSDAY FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE Left([PO_NO],2)='" & right(Budget,2) & "' and MS_PO.STATUS in ('5','9') ORDER BY MS_PO.REAL_PO;"
	
elseif SelectedDate <> "" then 
	
	SelectedDate = right(SelectedDate,4) & mid(SelectedDate,4,2) & left(SelectedDate,2)
	'response.write(selectedDate)
	sql = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, MS_PO.STATUS, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BILLOUTACC, BILLOUTFIN, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME, FIRST_RCVDATE, BILL_END_ACC, APPROVE_NO_FIN, CASE WHEN BILL_PAY_FIN IS NULL THEN DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) ELSE DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) END AS PROCESSDAY, (select max(DATE_RECEIVE) from MS_IVO where PO_NO=MS_PO.REAL_PO) as DateRCV FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE convert(varchar,BILLOUT,112)='" & SelectedDate & "' ORDER BY MS_PO.REAL_PO;"

end if 

Set PO = Server.CreateObject("ADODB.Recordset")
PO.Open sql, Conn, 1,3


Response.charset="windows-874"
%>



<table class="table table-bordered table-sm" width="100%" cellspacing="0">
  <thead>
  <tr>
    <th width="8%" ><div align="center">รหัสใบสั่งซื้อ</div></th>
    <th width="8%" ><div align="center">วันที่ออกใบสั่งซื้อ</div></th>
    <th width="7%" ><div align="center">มูลค่า</div></th>
    <th width="29%" ><div align="center">บริษัท</div></th>
    <th width="6%" ><div align="center">ตรวจรับแล้ว</div></th>
	<%if SelectedDate <>"" then  %>
	<th width="9%" ><div align="center">วันที่ตรวจรับ</div></th>
	<%end if%>
    <th width="9%" ><div align="center">ส่งตั้งหนี้</div></th>
	<th width="9%" ><div align="center">ส่งเอกสาร</div></th>
    <th width="7%" ><div align="center">เลขที่ขออนุมัติ</div></th>
    <th width="7%" ><div align="center">ตัดจ่าย</div></th>
	<th width="6%" ><div align="center">จำนวนวัน</div></th>
    <th width="9%" ><div align="center">ปิดบัญชี</div></th>
  </tr>
  </thead>
  <tbody>
  <%  runno = 1
While NOT PO.EOF
%>
	
    <tr> 

		<td><div align="center"><a href="#" onclick="ShowPODetail('<%=PO("REAL_PO")%>')"><%=(PO("REAL_PO"))%></a></div></td>
		<td><div align="center"><%=PO("PO_DATE")%></div></td>
		<td><div align="center"><%=formatnumber(PO("TOTAL_COST"))%></div></td>
		<td><div align="left"><%=PO("COMPANY_NAME_PO")%></div></td>
		<td><div align="center">
			<%if PO("STATUS") <> "1" and PO("STATUS") <> "A" and PO("STATUS") <> "B" then
				response.write(formatnumber(PO("TOTAL_COST")))
			end if%>
		</div></td>
		<%if SelectedDate <>"" then  %>
			<td><div align="center"><%=PO("DateRCV")%></div></td>
		<%end if%>
		<td><div align="center"><%=PO("BILLOUTACC")%></div></td>
	    <td><div align="center"><%=PO("BILLOUTFIN")%></div></td>
	    <td><div align="center"><%=PO("APPROVE_NO_FIN")%></div></td>
	    <td><div align="center"></div></td>
		<td><div align="center"><%=PO("PROCESSDAY")%></div></td>
		<td><div align="center"><%=PO("BILL_END_ACC")%></div></td>
	      
<tr>
   
<%
  
  runno = runno +1
  PO.MoveNext()
Wend
%>
	</tbody>
</table>