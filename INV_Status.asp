<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->
<!--#include file="Connections/HOMC.asp" -->

<%
session("DEPT_ID") =""

Dim Drug
Dim Drug_numRows

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING

Function Urate(Number)
Dim A, B, C, D, E, F, G
		if number = 0 then
		urate = 1 
		else
		urate = number
		end if
End Function


' --- กำหนดเงื่อนไขเรียงลำดับ: รหัสยา -> ชื่อยา ---
' ถ้า WORKING_CODE เก็บเป็นตัวเลข ให้ CAST เป็น INT จะได้เรียงตัวเลขจริง
Dim orderBy
orderBy = " ORDER BY CAST(WORKING_CODE AS INT) ASC, DRUG_NAME COLLATE Thai_CI_AS ASC"
' หมายเหตุ: ถ้าเครื่องฐานข้อมูลไม่รองรับ COLLATE Thai_CI_AS ให้เปลี่ยนเป็น:
' orderBy = " ORDER BY CAST(WORKING_CODE AS INT) ASC, DRUG_NAME ASC"


If request.QueryString("search") <> "" Then 
    keyword = request.QueryString("search")
ElseIf request.QueryString("SearchMenu") <> "" Then 
    keyword = request.QueryString("SearchMenu")
End If

If keyword <> "" Then
    Drug.Source =  "SELECT INV_MD.* FROM INV_MD WHERE (INV_MD.NOUSE Is Null) AND ( " & _
                   "  ([drug_name]   Like '%" & Replace(keyword, "'", "''") & "%') OR " & _
                   "  ([composition] Like '%" & Replace(keyword, "'", "''") & "%') OR " & _
                   "  ([HOSP_CODE]   Like '%" & Replace(keyword, "'", "''") & "%') OR " & _
                   "  ([WORKING_CODE] Like '%" & Replace(keyword, "'", "''") & "%') " & _
                   ") " & orderBy
Else
    Drug.Source = "SELECT * FROM INV_MD WHERE [NOUSE] IS NULL " & orderBy
End If


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
%>
<%
function ShowHOMCName(invcode)
  Dim HOMCName
  Set HOMCName = Server.CreateObject("ADODB.Recordset")
  HOMCName.ActiveConnection = MM_HOMC_STRING
  HOMCName.Source = "SELECT [name] FROM Med_inv WHERE [code]='" & invcode & "' and site='1'"
  HOMCName.CursorType = 3
  HOMCName.CursorLocation = 3
  HOMCName.LockType = 1
  HOMCName.Open()

  if not HOMCName.eof then
    ShowHOMCName = CStr(HOMCName("name"))
  else
    ShowHOMCName = ""               ' ? สำคัญ: อย่าคืน Null
  end if

  HOMCName.Close
  Set HOMCName = Nothing
end function

%>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />

    <!-- jQuery (necessary for Bootstrap's JavaScript plugins) -->
    <!-- script src="http://code.jquery.com/jquery-latest.min.js"></script>
    <!-- Include all compiled plugins (below), or include individual files as needed -->
	

<title>ระบบรายงานปริมาณยาและเวชภัณฑ์คงคลัง</title>

    <!-- Bootstrap -->
    
    <link rel="stylesheet" href="css/bootstrap.min.css"> 
	<script src="js/jquery-3.2.1.min.js"></script>
	<script src="js/bootstrap.min.js"></script>
    <!-- HTML5 shim and Respond.js for IE8 support of HTML5 elements and media queries -->
    <!-- WARNING: Respond.js doesn't work if you view the page via file:// -->
    <!--[if lt IE 9]>
      <script src="https://oss.maxcdn.com/html5shiv/3.7.3/html5shiv.min.js"></script>
      <script src="https://oss.maxcdn.com/respond/1.4.2/respond.min.js"></script>
    <![endif]-->
    <!--#include file="menu.asp" -->
  </head>
<body>
<p>
<div class="container" align="center">
		<div class="row">
        
  <!--form id="form1" name="form1" method="get" action="default.asp">
      <label>
      <div align="center">ค้นหาข้อมูล 
        <input name="Search" type="text" id="Search" size="50" placeholder="รหัสยา ชื่อสามัญทางยา ชื่อการค้า และกลุ่มยา" /> 
        <input type="submit" class="btn btn-primary" name="Submit" value="Submit" />
      
      </label>
      </div>
  </form-->
            <div align="center">
              <!--label>
              (สามารถค้นหาได้จาก รหัสยา ชื่อสามัญทางยา ชื่อการค้า และกลุ่มยา)
              
              </label-->    
            </div>
    <h1 align="center">

</h1>

<% if keyword <>"" then
response.write("ผลการค้นหา ( ")
response.write(keyword) 
response.write(" ) พบ ")
response.write(drug.recordcount) 
response.write(" รายการ")
end if
%>       
                
                
<table class = "table" width="100%" align="center" >
    <thead>
		<tr>
			<th width="60%" ><div align="center">ชื่อยา</div></th>
			<th width="3%" ><div align="center">VEN</div></th>
			<th width="5%" ><div align="center">รหัสยา</div></th>
			<th width="5%" ><div align="center">&#3588;&#3591;&#3588;&#3621;&#3633;&#3591;</div></th>
			<th width="5%" ><div align="center">&#3627;&#3609;&#3656;&#3623;&#3618;</div></th>
			<th width="5%" ><div align="center">Location</div></th>
			<th width="5%" ><div align="center">Rate เดือนก่อน </div></th>
			<th width="5%" >ยืมได้</th>
			<th width="5%" ><div align="center">&#3648;&#3627;&#3621;&#3639;&#3629;&#3651;&#3594;&#3657;&#3652;&#3604;&#3657; (&#3648;&#3604;&#3639;&#3629;&#3609;) </div></th>
		</tr>
	</thead>
  <tbody>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT Drug.EOF)) 
%>
<%
		if isnull(drug("RATE_PER_MONTH")) = false then
			A= CSng(drug("RATE_PER_MONTH")) 
		else
			A = 0 
		end if 
		
	  If isnull(drug("QTY_ON_HAND")) = false  then 
	  	Q = CSng(drug("QTY_ON_HAND"))
	  else 
	  	Q = 0
	  end if 
		 if  isnumeric(A) = true and isnumeric(Q) = true then
			if A <> 0 then 
				B= round(Q/A,2)
				'C = B
						if B<1 and B>0 then
						B = "0" & B
						end if
			else
				B = "N/A"
			end if 
		 else 
		 B = "N/A"
		'C = round(drug("QTY_ON_HAND")/A,2)
		
		end if		
		
Dim Borrow
Dim Borrow_numRows

Set Borrow = Server.CreateObject("ADODB.Recordset")
Borrow.ActiveConnection = MM_INVFlood_STRING
Borrow.Source = "SELECT     RECORD_NUMBER, WORKING_CODE, BORROW_QTY, (SELECT     SUM(BORROW_QTY) AS SUM_QTY FROM BORROW WHERE (VCAR_CODE = VCAR.RECORD_NUMBER) AND (WORKING_CODE = VCAR.WORKING_CODE)) AS QTY FROM VCAR WHERE     (BORROW_QTY <> 0) AND (CLOSE_STATUS <> 'C' AND WORKING_CODE='" & Drug("WORKING_CODE") & "')"

Borrow.CursorType = 3
Borrow.CursorLocation = 3
Borrow.LockType = 1
Borrow.Open()

Borrow_numRows = 0		
		
if not borrow.eof then
	if ISNULL(Borrow("QTY")) = True then
		BQ = 0 
	else
		BQ = Borrow("QTY")
	end if 		
	BO = Borrow("BORROW_QTY") - BQ
else
	BO = 0
end if
%>
<%
' ==== คำนวณชื่อยาให้ชัวร์ (กัน Null) ====
Dim baseName, hospName, displayName
baseName   = "" & Drug("DRUG_NAME")                ' กัน Null
hospName   = ShowHOMCName("" & Drug("HOSP_CODE"))  ' ฟังก์ชันจะคืน "" ถ้าไม่เจอ
displayName = Server.HTMLEncode(baseName)
If Len(Trim(hospName)) > 0 Then
  displayName = displayName & " (" & Server.HTMLEncode(hospName) & ")"
End If
%>

<tr>  <!-- ? สำคัญ: ใส่ <tr> เปิดแถวให้ครบ -->

  <!-- ชื่อยา -->
  <td>
    <%= runno %>. 
    <a href="chkstock.asp?code=<%= Drug("WORKING_CODE") %>">
      <%= displayName %>
    </a>
  </td>

  <!-- VEN -->
  <td><div align="center"><%= Drug("VEN") %></div></td>

  <!-- รหัส (WORKING_CODE) + HOSP_CODE -->
  <td><div align="center"><%= Drug("WORKING_CODE") & " (" & ("" & Drug("HOSP_CODE")) & ")" %></div></td>

  <!-- คงคลัง -->
  <td><div align="center"><%= Drug("QTY_ON_HAND") %></div></td>

  <!-- หน่วย -->
  <td><div align="center"><%= Drug("SALE_UNIT") %></div></td>

  <!-- Location -->
  <td><div align="center"><%= Drug("LOCATION") %></div></td>

  <!-- Rate ต่อเดือน -->
  <td><div align="center"><%= Drug("RATE_PER_MONTH") %></div></td>

  <!-- ยืมคงเหลือ (BO) -->
  <td><div align="center"><%= BO %></div></td>

  <!-- เหลือใช้ได้ (เดือน) (B) -->
  <td><div align="center"><%= B %></div></td>
</tr>

<%
' ปิด/คืน Borrow ทุกรอบ กันรั่วทรัพยากร
Borrow.Close
Set Borrow = Nothing
%>


<%
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  Drug.MoveNext()
Wend
%>
</tbody>
</table>
                


				<!-- Button -->

				<!--button type="button" class="btn btn-default" >HOME</button>
				<a class="btn btn-primary" role="button" href="INV_Status.asp">รายงานสถานะคงคลัง</a>
				<a class="btn btn-success" role="button" href="PO_search.asp">ค้นหาใบสั่งซื้อ</a>
				<a class="btn btn-info" role="button" href="pending.asp">รายการค้างจ่าย</a>
				<a class="btn btn-warning" role="button" href="ShelfList.asp" >รายการตามชั้นเก็บ</a>
				<a class="btn btn-danger" role="button" href="Findgen.asp">Abbr checking</a>
				
				<br /><br/ >
				
				<!-- Table -->
                
                
				<!-- Form -->
				<!--form>
				  <div class="form-group">
					<label for="email">Email address:</label>
					<input type="email" class="form-control" id="email">
				  </div>
				  <div class="form-group">
					<label for="pwd">Password:</label>
					<input type="password" class="form-control" id="pwd">
				  </div>
				  <div class="checkbox">
					<label><input type="checkbox"> Remember me</label>
				  </div>
				  <button type="submit" class="btn btn-default">Submit</button>
				</form>

				<!-- Glyphicon  -->
				<!--p>Envelope icon: <span class="glyphicon glyphicon-envelope"></span></p> 
				<p>Envelope icon as a link:
				  <a href="#"><span class="glyphicon glyphicon-envelope"></span></a>
				</p>
				<p>Search icon: <span class="glyphicon glyphicon-search"></span></p>
				<p>Search icon on a button:
				  <button type="button" class="btn btn-default">
					<span class="glyphicon glyphicon-search"></span> Search
				  </button>
				</p>
				<p>Search icon on a styled button:
				  <button type="button" class="btn btn-info">
					<span class="glyphicon glyphicon-search"></span> Search
				  </button>
				</p>
				<p>Print icon: <span class="glyphicon glyphicon-print"></span></p> 
				<p>Print icon on a styled link button:
				  <a href="#" class="btn btn-success btn-lg">
					<span class="glyphicon glyphicon-print"></span> Print 
				  </a>
				</p-->



	  </div>
  </div>

  </body>
</html>

<%
Drug.Close()
Set Drug = Nothing
%>