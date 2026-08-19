import { spawn } from "node:child_process";

const name = "dsh-port-control";
const inject = ["commands", "webServer"];
const CONTROL_PREFIX = "/__dsh-control/";

function json(res, status, value) {
  const body = JSON.stringify(value);
  res.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
    "content-length": Buffer.byteLength(body)
  });
  res.end(body);
}

function html(res, status, body) {
  res.writeHead(status, {
    "content-type": "text/html; charset=utf-8",
    "cache-control": "no-store",
    "content-length": Buffer.byteLength(body)
  });
  res.end(body);
}

function stopCurrentProcess() {
  setTimeout(() => process.exit(0), 150);
}

function restartCurrentProcess() {
  const args = process.argv.slice(1);
  setTimeout(() => {
    const child = spawn(process.execPath, args, {
      cwd: process.cwd(),
      env: { ...process.env },
      detached: true,
      stdio: "ignore",
      windowsHide: true
    });
    child.unref();
  }, 750);
  setTimeout(() => process.exit(0), 900);
}

function commandResult(action) {
  if (action === "stop") {
    stopCurrentProcess();
    return { kind: "success", text: "dsh 服务正在关闭，当前服务端口将释放。" };
  }
  restartCurrentProcess();
  return { kind: "success", text: "dsh 服务正在重启，当前服务端口会短暂释放后重新监听。" };
}

function apply(ctx) {
  ctx.effect(() => ctx.commands.register({
    name: "dsh-stop",
    description: "关闭当前 dsh 服务并释放服务端口",
    recordInput: false,
    handler: () => commandResult("stop")
  }), "dsh-port-control: stop command");

  ctx.effect(() => ctx.commands.register({
    name: "dsh-restart",
    description: "重启当前 dsh 服务并重新监听服务端口",
    recordInput: false,
    handler: () => commandResult("restart")
  }), "dsh-port-control: restart command");

  for (const action of ["stop", "restart"]) {
    ctx.effect(() => ctx.webServer.register({
      kind: "exact",
      path: `${CONTROL_PREFIX}${action}`,
      handler: async (req, res) => {
        if (req.method !== "GET" && req.method !== "POST") {
          json(res, 405, { ok: false, error: "method_not_allowed" });
          return;
        }
        json(res, 202, { ok: true, action, port: ctx.webServer.port });
        if (action === "stop") stopCurrentProcess();
        else restartCurrentProcess();
      }
    }), `dsh-port-control: /${action} route`);
  }

  ctx.effect(() => ctx.webServer.register({
    kind: "exact",
    path: `${CONTROL_PREFIX}new-session`,
    handler: async (req, res) => {
      if (req.method !== "GET") {
        json(res, 405, { ok: false, error: "method_not_allowed" });
        return;
      }
      html(res, 200, "<!doctype html><meta charset=\"utf-8\"><script>try{localStorage.removeItem('dsh.sessions.current')}catch(e){};location.replace('/');</script>");
    }
  }), "dsh-port-control: new session route");
}

export { apply, inject, name };
