<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>



<%

	session("NameOfUser") = ""
	session("UserID") = ""
	
	if request.form("FromPage") = "2ndFloor" then
		
	else
		response.redirect("index.html?flag=1")
	end if
%>