const path = require("path");

const appCommand = process.env.HAGICODE_APP_COMMAND;
const appArguments = (process.env.HAGICODE_APP_ARGUMENTS || "")
  .split(" ")
  .map((part) => part.trim())
  .filter(Boolean);

if (!appCommand) {
  throw new Error("HAGICODE_APP_COMMAND is required for pm2 startup");
}

module.exports = {
  apps: [
    {
      name: "omniroute",
      script: "omniroute",
      args: "",
      cwd: "/app",
      autorestart: true,
      exec_mode: "fork",
      instances: 1,
      max_restarts: 20,
      env: {
        HOME: process.env.HOME,
      },
    },
    {
      name: "hagicode-app",
      script: path.join(__dirname, "wait-for-ready.sh"),
      args: [appCommand, ...appArguments],
      cwd: "/app",
      autorestart: true,
      exec_mode: "fork",
      instances: 1,
      max_restarts: 20,
      env: {
        HOME: process.env.HOME,
      },
    },
  ],
};
