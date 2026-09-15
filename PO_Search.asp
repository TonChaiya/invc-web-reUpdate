<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Dim Drug
Dim Drug_numRows

YYY = request.QueryString("YYY")
POStatus = request.QueryString("POstatus")

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING

Drug.Source = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE STATUS='" & POstatus & "' and Left(MS_PO.PO_NO,2)='" & YYY & "' ORDER BY MS_PO.REAL_PO;"

Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()

Drug_numRows = 0

Dim Repeat1__numRows
Dim Repeat1__index

Repeat1__numRows = -1
Repeat1__index = 0
Drug_numRows = Drug_numRows + Repeat1__numRows

Budget = request.querystring("FiscalYear")
'response.write(Budget)
if Budget <> "" then 

Set dateList = Server.CreateObject("ADODB.Recordset")

dateList.ActiveConnection = MM_INVFlood_STRING
dateList.Source = "select distinct convert(varchar,BILLOUT,103) as Bill_out, BILLOUT from MS_PO WHERE Left([PO_NO],2)='" & right(Budget,2) & "' and BILLOUT is not null order by BILLOUT desc"
dateList.CursorType = 3
dateList.CursorLocation = 3
dateList.LockType = 1
dateList.Open()

end if 
Response.charset="windows-874"
%>


<% showMenu = request.querystring("showMenu")
	if showMenu = "" then %>
	
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ค้นหาใบสั่งซื้อ</title>

    <!-- Bootstrap -->
    
    <link rel="stylesheet" href="css/bootstrap.min.css">
    
    <!-- HTML5 shim and Respond.js for IE8 support of HTML5 elements and media queries -->
    <!-- WARNING: Respond.js doesn't work if you view the page via file:// -->
    <!--[if lt IE 9]>
      <script src="https://oss.maxcdn.com/html5shiv/3.7.3/html5shiv.min.js"></script>
      <script src="https://oss.maxcdn.com/respond/1.4.2/respond.min.js"></script>
    <![endif]-->
   <!--#include file="menu.asp" -->
  </head>

<body>
<%end if%>


<div class="container" align="center">
<div class="row">
        
<br /><br /><br /><br /><br />

<div align="center">
  <form id="form1" name="form1" method="get" action="POdetail.asp">
      <table width="100%" border="0" align="center" cellpadding="0" cellspacing="0">
        <tr>
          <th>เลขที่ใบสั่งซื้อ : </th>
          <th><input name="RPO" type="text" id="RPO" maxlength="10" /></th>
          <th>
		  <%if showMenu = "" then %>
			<input name="ค้นหา" type="submit" class="btn btn-primary" id="ค้นหา" value="ค้นหา" />
		  <%else%>	
			<button type = "button" class="btn btn-primary" onclick="PO_detail_in_dashboard()">ค้นหา</button>
		  <%end if%>
		  </th>
		  
		  <%if Budget<>"" then%>
		  <th>วันที่ส่งเอกสารให้การเงิน</th>
		  <th> 
				<select name="SelectedDate" id="SelectedDate">
					<option disabled selected>--เลือกวันที่ส่งเอกสาร--</option>
					<%while not dateList.eof%>
						<option value="<%=dateList("Bill_out")%>"><%=dateList("Bill_out")%></option>	
					<%dateList.movenext
					wend%>
				</select>
				<button type = "button" class="btn btn-primary" onclick="PO_detail_in_dashboard_BillOutFin()">ค้นหา</button>
		  </th>
		  <%end if %>
        </tr>
      </table>
  </form>
  
</div>
</div>
<br />

<% showMenu = request.querystring("showMenu")
	if showMenu = "" then %>
	
  <script src="css/bootstrap.min.css"></script>
  <script src="js/bootstrap.min.js"></script>
  <script src="js/jquery-3.2.1.min.js"></script>


</body>
</html>
<%end if%>

<%
Drug.Close()
Set Drug = Nothing
%>

