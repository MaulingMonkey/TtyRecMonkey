#include "putty.h"
#include "puttymem.h"
#include "terminal.h"

#define EXPORT(rt) __declspec(dllexport) rt

EXPORT(Terminal*) CreatePuttyTerminal( int w, int h ) {
	int i;
	Config config = {0};
	struct unicode_data* unicode;
	Terminal* terminal;

	unicode = snew(struct unicode_data);
	memset( unicode, 0, sizeof(struct unicode_data) );

	for ( i=0 ; i<256 ; ++i ) {
		unicode->unitab_ctrl[i]=i;
		//unicode->unitab_line[i]=i;
	}

	terminal = term_init( &config, unicode, NULL );
	term_size( terminal, h, w, 0 );

	return terminal;
}

EXPORT(termchar*) GetPuttyTerminalLine( Terminal* terminal, int y, int unused ) {
	termline* line = index234(terminal->screen,y);
	return line->chars;
}

EXPORT(void) DestroyPuttyTerminal( Terminal* terminal ) {
	safefree( terminal->ucsdata );
	term_free(terminal);
}

EXPORT(void) SendPuttyTerminal( Terminal *terminal, int is_stderr, const char *data, int len ) {
	term_data( terminal, is_stderr, data, len );
}
