// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

const { ComponentDialog, DialogTurnStatus } = require('botbuilder-dialogs');

class CancelAndHelpDialog extends ComponentDialog {
    async onBeginDialog(innerDc, options) {
        const result = await this.interrupt(innerDc);
        if (result) {
            return result;
        }
        return await super.onBeginDialog(innerDc, options);
    }

    async onContinueDialog(innerDc) {
        const result = await this.interrupt(innerDc);
        if (result) {
            return result;
        }
        return await super.onContinueDialog(innerDc);
    }

    async interrupt(innerDc) {
        const text = innerDc.context.activity.text.toLowerCase();

        switch (text) {
        case 'help':
        case '?':
            await innerDc.context.sendActivity('Show Help...');
            return { status: DialogTurnStatus.waiting };
        case 'cancel':
        case 'quit':
            await innerDc.context.sendActivity('Canceling');
            return await innerDc.cancelAllDialogs();
        }
    }
}

module.exports.CancelAndHelpDialog = CancelAndHelpDialog;
