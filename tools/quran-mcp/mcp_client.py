import json,sys,urllib.request

URL="https://mcp.quran.ai"
class C:
    def __init__(self):
        self.sid=None; self.id=0
    def rpc(self,method,params=None,notify=False):
        self.id+=1
        body={"jsonrpc":"2.0","method":method}
        if not notify: body["id"]=self.id
        if params is not None: body["params"]=params
        h={"Content-Type":"application/json","Accept":"application/json, text/event-stream"}
        if self.sid: h["mcp-session-id"]=self.sid
        req=urllib.request.Request(URL,data=json.dumps(body).encode(),headers=h,method="POST")
        with urllib.request.urlopen(req,timeout=60) as r:
            if not self.sid:
                self.sid=r.headers.get("mcp-session-id")
            raw=r.read().decode()
        if notify: return None
        for line in raw.splitlines():
            if line.startswith("data:"):
                return json.loads(line[5:].strip())
        return json.loads(raw) if raw.strip() else None
    def init(self):
        r=self.rpc("initialize",{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"athkar","version":"1"}})
        self.rpc("notifications/initialized",notify=True)
        return r
    def call(self,name,args):
        r=self.rpc("tools/call",{"name":name,"arguments":args})
        if "error" in r: return {"__error__":r["error"]}
        out=[]
        for c in r.get("result",{}).get("content",[]):
            if c.get("type")=="text": out.append(c["text"])
        txt="\n".join(out)
        try: return json.loads(txt)
        except Exception: return txt

    def call_struct(self, name, args):
        """Return structuredContent when present, else parsed text content."""
        r = self.rpc("tools/call", {"name": name, "arguments": args})
        if "error" in r:
            raise RuntimeError(r["error"])
        res = r.get("result", {})
        if res.get("structuredContent") is not None:
            return res["structuredContent"]
        txt = "\n".join(c["text"] for c in res.get("content", []) if c.get("type") == "text")
        try:
            return json.loads(txt.splitlines()[0])
        except Exception:
            return txt
